# 플레이어 소지 물건 액션 및 NPC 투척 개발계획서

## 1. 목적

플레이어가 물건을 들고 다른 NPC에게 이동했을 때 현재 NPC의 물건 액션뿐 아니라 플레이어가 들고 있는 물건의 액션도 함께 표시한다.

이번 구현 범위는 다음과 같다.

- 들고 있는 물건 내려놓기
- 들고 있는 물건 부수기
- 들고 있는 물건을 현재 NPC에게 던지기
- 투척 오브젝트 회전 및 이동
- 대상 NPC와 충돌 시 물건 파괴 효과
- 대상 NPC 쓰러짐 표시
- 쓰러진 NPC는 게임 세션 동안 복구하지 않음

다음 액션은 제외한다.

- NPC에게 보여주기
- NPC에게 건네기
- 원래 소유자에게 돌려주기

전체 구현 순서는 다음과 같이 고정한다.

```text
1. Backend 모델·액션·정책 구현 및 테스트
2. Unity 액션 UI·Drop·Throw·NPC 쓰러짐 구현 및 Play Mode 검증
3. Backend API 계약 확정
4. Web Frontend 타입·UI·애니메이션 반영 및 브라우저 검증
```

웹 프론트엔드는 Backend와 Unity 동작이 확정된 뒤 마지막 단계에서 적용한다.

## 2. 현재 구조와 문제

Backend 키보드를 든 상태로 PM 위치에 가면 서버에는 이미 다음 액션이 존재한다.

```text
Break Backend keyboard
Drop Backend keyboard
Pick up PM keyboard
Inspect Release document
Pick up Release document
```

그러나 Unity 액션창은 다음 조건으로 필터링한다.

```csharp
action.target_id == currentTarget.TargetId
```

Backend 키보드의 Drop/Break 액션은 `target_id=backend_01`이고 현재 대상은 `pm_01`이므로 Unity 화면에서 제외된다.

또한 현재 `target_id`는 실제 행동 대상보다 물건 소유자 의미로 사용되고 있다. NPC에게 던지기를 구현하려면 다음 값을 구분해야 한다.

- `target_id`: 투척 대상 NPC
- `owner_id`: 물건의 원래 소유자
- `object_id`: 사용한 물건
- `scope`: 액션이 현재 NPC 대상인지, 플레이어 소지품 자체의 액션인지

## 3. 목표 UX

PM 액션창 예시:

```text
PM에게 할 수 있는 행동
  - PM keyboard 집기
  - Release document 조사하기

내가 들고 있는 물건: Backend keyboard
  - 바닥에 내려놓기
  - 부수기
  - PM에게 던지기
```

투척 흐름:

```text
Throw Backend keyboard at PM 클릭
        |
        v
서버 액션 검증 및 상태 변경
        |
        v
플레이어 손 위치에서 키보드 투사체 생성
        |
        v
키보드가 회전하며 PM 방향으로 이동
        |
        v
PM 충돌
        |
        +-- 키보드 두 조각 파괴 효과
        +-- PM 스프라이트 옆으로 회전
        +-- PM 상호작용 비활성화
        +-- 서버 is_fallen=true 유지
```

## 4. 백엔드 데이터 모델 변경

### 4.1 GameActionFamily 확장

`throw_held_object`는 타입에는 이미 존재하지만 액션 생성과 실행이 구현되지 않았다. 해당 타입을 실제 액션으로 활성화한다.

### 4.2 AvailableGameAction 필드 분리

대상 파일:

- `backend/app/models.py`

권장 필드:

```python
class AvailableGameAction(BaseModel):
    id: str
    family: GameActionFamily
    label: str
    object_id: str | None = None
    target_id: str | None = None
    owner_id: str | None = None
    scope: Literal["target", "held_item", "world"] = "world"
    location: str
    enabled: bool = True
    disabled_reason: str | None = None
```

의미:

| scope | 용도 |
|---|---|
| `target` | 현재 NPC에게 향하는 액션. 예: PM에게 던지기 |
| `held_item` | 현재 NPC와 무관한 소지품 액션. 예: Drop, Break |
| `world` | 현재 위치의 물건 액션. 예: Pick up, Inspect |

### 4.3 NPC 쓰러짐 상태

`NPCState` 또는 `DynamicState`에 다음 필드를 추가한다.

```python
is_fallen: bool = False
```

쓰러진 NPC 규칙:

- 세션 종료 전까지 `true` 유지
- 정상 대화 불가
- 새로운 투척 대상에서 제외
- 감정·Stress 등 기존 상태는 보존
- 리셋 또는 새 세션에서만 정상화

## 5. 서버 액션 생성

대상 파일:

- `backend/app/game/action_registry.py`

### 5.1 소지품 공통 액션

플레이어가 물건을 들고 있으면 현재 NPC와 관계없이 생성한다.

```text
break_backend_keyboard
drop_backend_keyboard
```

이 액션들은 다음 값을 사용한다.

```text
scope=held_item
target_id=null
owner_id=backend_01
```

### 5.2 NPC 대상 투척 액션

현재 위치에 있는 쓰러지지 않은 NPC마다 투척 액션을 생성한다.

예시:

```text
throw_backend_keyboard_at_pm_01
throw_backend_keyboard_at_qa_01
```

액션 데이터:

```text
family=throw_held_object
scope=target
object_id=backend_keyboard
target_id=pm_01
owner_id=backend_01
```

검증 조건:

- 물건의 `holder_id == player`
- 물건이 파괴되지 않음
- 대상 NPC가 현재 위치에 있음
- 대상 NPC가 쓰러지지 않음
- 플레이어 자신은 대상이 될 수 없음

## 6. 서버 투척 처리

대상 파일:

- `backend/app/game/engine.py`

`throw_held_object` 성공 시 서버 상태를 먼저 확정한다.

```text
물건 holder_id = null
물건 condition = destroyed
물건 location = 현재 위치
대상 NPC is_fallen = true
플레이어 inventory에서 물건 제거
```

응답 메시지 예시:

```text
Backend keyboard을(를) PM / Planner에게 던졌습니다. 물건이 파손됐고 PM / Planner가 쓰러졌습니다.
```

### 6.1 사회적 영향

투척은 두 사건이 결합된 행동이다.

1. 대상 NPC에 대한 신체 공격
2. 원래 소유자 물건의 파괴

권장 정책:

- 대상 NPC: `physical_assault`, severity 5
- 원래 소유자: `property_aggression`, severity 4
- 같은 위치 NPC: 목격자
- 대상과 소유자가 같으면 중복 효과를 합쳐 한 번만 적용

예상 결과:

- 대상 NPC: `afraid` 또는 `shocked`, Stress 크게 증가, 대화 거부
- 물건 소유자: `angry`, grievance 증가
- 목격자: `shocked`, 작은 Stress 증가
- `security_called`, `hr_escalated`, `dialogue_refused` 이벤트 기록

## 7. Unity DTO 변경

대상 파일:

- `Assets/OfficeMVP/Scripts/Backend/OfficeBackendClient.cs`

추가 필드:

```csharp
public sealed class OfficeAvailableGameActionDto
{
    public string owner_id;
    public string scope;
}

public sealed class OfficeNpcDto
{
    public bool is_fallen;
}
```

서버 필드명을 그대로 사용해 `JsonUtility`가 직접 파싱하도록 한다.

## 8. 액션 UI 변경

대상 파일:

- `Assets/OfficeMVP/Scripts/Interaction/OfficeInteractionUI.cs`

현재 단일 필터를 다음 두 그룹으로 변경한다.

### 8.1 현재 NPC 액션

```csharp
action.scope == "target" && action.target_id == currentTarget.TargetId
```

또는 현재 NPC가 소유한 월드 액션:

```csharp
action.scope == "world" && action.target_id == currentTarget.TargetId
```

### 8.2 플레이어 소지품 액션

```csharp
action.scope == "held_item"
```

현재 NPC와 무관하게 항상 별도 섹션에 표시한다.

섹션 제목 예시:

```text
PM 관련 액션
내가 들고 있는 물건: Backend keyboard
```

## 9. Drop 위치 표현 수정

현재 Drop은 서버 `location`을 바꾸지만 Unity 오브젝트 뷰는 원래 책상 위치에 고정돼 있다. Backend keyboard를 PM 위치에서 Drop하면 Backend 책상에서 다시 나타날 수 있다.

대상 파일:

- `Assets/OfficeMVP/Scripts/World/OfficeWorldObjectView.cs`
- `Assets/OfficeMVP/Scripts/World/OfficeWorldObjectStatePresenter.cs`
- 필요 시 `Assets/OfficeMVP/Scripts/Core/OfficeMvpBootstrap.cs`

MVP에서는 위치별 Drop Anchor를 사용한다.

| location | Unity Drop Anchor |
|---|---|
| `meeting_room` | 회의실 중앙 바닥 |
| `dev_area` | 개발 구역 통로 |
| `qa_desk` | QA 책상 앞 |
| `pm_desk` | PM 책상 앞 |

규칙:

- 원래 위치에 있고 소지자가 없으면 원래 책상 위치 표시
- 다른 위치에서 Drop되면 해당 구역 Drop Anchor로 이동
- 다시 집으면 월드 표시 숨김
- 파괴되면 월드 표시 제거

## 10. Unity 투척 오브젝트

신규 파일 권장:

- `Assets/OfficeMVP/Scripts/World/OfficeThrownObjectProjectile.cs`
- `Assets/OfficeMVP/Scripts/World/OfficeThrowCoordinator.cs`

### 10.1 투사체 생성

투척 요청 전에 다음 시각 정보를 캐시한다.

- 현재 들고 있는 오브젝트 Sprite
- 플레이어 손 위치
- 표시 Scale
- 대상 NPC Transform

서버 성공 응답 후 캐시한 데이터로 투사체 복제본을 생성한다. 서버 snapshot 적용으로 손의 키보드가 먼저 사라져도 투사체 생성에 문제가 없도록 한다.

### 10.2 이동 방식

물리 충돌의 예측 불가능성을 줄이기 위해 MVP에서는 대상 전용 Kinematic 투사체를 사용한다.

- 시작점: 플레이어 손 위치
- 목표점: 대상 NPC 몸통 위치
- 이동 속도: 약 6~8 world units/sec
- 회전 속도: 약 720~1080도/sec
- Collider2D는 Trigger
- 지정 대상 NPC만 충돌 처리
- 책상·가림막·다른 NPC는 통과
- 최대 비행 시간 초과 시 목표 위치에서 강제 충돌 처리

`Update()` 또는 Coroutine에서 다음을 수행한다.

```text
position = MoveTowards(position, targetPosition, speed * deltaTime)
rotation += spinSpeed * deltaTime
```

### 10.3 충돌 처리

대상 NPC의 `InteractablePoint`와 충돌하면:

1. 투사체 이동 중지
2. `OfficeBreakEffect` 실행
3. 키보드를 좌우 두 조각으로 분리
4. 투사체 제거
5. NPC Fall 애니메이션 실행

## 11. NPC 쓰러짐 표현

신규 파일 권장:

- `Assets/OfficeMVP/Scripts/Interaction/OfficeNpcFallView.cs`

NPC 루트 Transform을 직접 90도 회전하면 직업명과 감정 라벨까지 함께 눕는다. 따라서 루트는 유지하고 시각 Sprite만 별도로 눕힌다.

권장 처리:

1. 현재 NPC SpriteRenderer의 Sprite 복제
2. 원래 SpriteRenderer 비활성화
3. `FallenVisual` 자식 생성
4. `FallenVisual.localRotation.z = 90`
5. 바닥에 맞도록 localPosition 보정
6. CapsuleCollider2D 비활성화
7. 상호작용 Trigger 비활성화
8. `InteractablePoint` 비활성 상태 처리
9. 역할명·감정 라벨은 똑바로 유지하거나 `쓰러짐` 표시

복구 로직은 구현하지 않는다.

### 11.1 상호작용 차단

`PlayerInteractionDetector`가 쓰러진 NPC를 선택하지 않도록 한다.

```csharp
if (!point.isActiveAndEnabled || point.IsFallen)
{
    continue;
}
```

서버도 쓰러진 NPC에 대한 대화와 추가 투척을 거부한다.

## 12. 서버 응답과 애니메이션 타이밍

서버 snapshot은 액션 성공 즉시 다음 상태를 포함한다.

- 물건 파괴
- 인벤토리 제거
- NPC 쓰러짐

Unity가 snapshot을 즉시 적용하면 투사체가 날아가기 전에 손의 물건과 NPC가 사라지거나 쓰러질 수 있다.

이를 막기 위해 `OfficeThrowCoordinator`가 시각 상태를 조정한다.

권장 순서:

```text
1. 버튼 클릭
2. 손 위치/Sprite/대상 위치 캐시
3. 서버 요청
4. 성공 snapshot 수신
5. 월드 상태는 저장하되 NPC Fall 표현은 대기
6. 투사체 애니메이션 시작
7. 대상 충돌
8. BreakEffect + FallView 실행
9. 최종 snapshot 표현 적용 완료
```

서버 실패 시 투사체를 생성하지 않고 기존 소지 상태를 유지한다.

## 13. 웹 프론트엔드 반영

Backend와 Unity 구현 및 검증이 완료된 뒤 동일한 API 계약을 웹 프론트엔드에 반영한다.

대상 경로:

- `/Users/switch/Development/Web/Office_Agent_MVP/frontend/src/types.ts`
- `/Users/switch/Development/Web/Office_Agent_MVP/frontend/src/api.ts`
- `/Users/switch/Development/Web/Office_Agent_MVP/frontend/src/App.tsx`
- `/Users/switch/Development/Web/Office_Agent_MVP/frontend/src/styles.css`

### 13.1 TypeScript 타입 반영

`AvailableGameAction` 타입에 다음 필드를 추가한다.

```ts
owner_id?: string | null;
scope: "target" | "held_item" | "world";
```

NPC 상태 타입에 다음 필드를 추가한다.

```ts
is_fallen: boolean;
```

Backend 응답 타입과 Frontend 타입을 일치시키고 임의의 클라이언트 기본값으로 서버 필드를 덮어쓰지 않는다.

### 13.2 웹 액션 UI 구성

웹 액션 영역도 Unity와 동일하게 두 그룹으로 나눈다.

```text
현재 NPC 관련 액션
  - 현재 NPC의 물건 Pick up/Inspect
  - 현재 NPC 대상 Throw

내가 들고 있는 물건
  - Drop
  - Break
```

필터 규칙:

```ts
const targetActions = actions.filter(
  (action) =>
    (action.scope === "target" || action.scope === "world") &&
    action.target_id === selectedNpcId,
);

const heldItemActions = actions.filter(
  (action) => action.scope === "held_item",
);
```

액션 버튼은 서버의 `enabled`와 `disabled_reason`을 그대로 반영한다.

### 13.3 웹 Throw 애니메이션

Unity의 물리·Collider 표현을 그대로 복제하지 않고 DOM/CSS 기반으로 표현한다.

권장 흐름:

1. Throw 버튼 클릭 전 플레이어·대상 NPC DOM 위치 측정
2. Backend 액션 요청 전송
3. 성공 응답 확인
4. 키보드 오버레이 요소 생성
5. 플레이어 위치에서 대상 NPC 위치까지 이동
6. 이동 중 CSS `rotate()`로 연속 회전
7. 대상 도착 시 키보드 두 조각 효과
8. 대상 NPC에 쓰러짐 클래스 적용

CSS 예시 구조:

```css
.thrown-object {
  position: fixed;
  pointer-events: none;
  z-index: 1000;
}

.npc--fallen {
  transform: rotate(90deg);
  transform-origin: center bottom;
}
```

실제 이동 좌표는 `getBoundingClientRect()`와 Web Animation API 또는 `requestAnimationFrame()`으로 계산한다.

### 13.4 웹 NPC 쓰러짐 상태

`is_fallen=true`인 NPC는 다음처럼 표시한다.

- 캐릭터 이미지를 옆으로 90도 회전
- `쓰러짐` 배지 표시
- 대화 버튼 비활성화
- 액션 대상에서 제외
- 페이지 상태 갱신 후에도 snapshot 기준으로 쓰러진 상태 유지
- 세션 reset 또는 새 세션에서만 정상화

### 13.5 웹 Drop/Break 결과

- Drop 성공 시 플레이어 인벤토리에서 제거
- 새 `location`에 물건이 나타나도록 상태 카드 갱신
- Break 성공 시 물건 상태를 `destroyed`로 표시
- 파괴된 물건의 Pick up/Inspect 버튼 제거
- 서버 응답 메시지는 액션 목록 갱신으로 덮어쓰지 않고 별도 결과 영역에 유지

### 13.6 웹 구현 순서

1. Backend OpenAPI/응답 계약 확정
2. `types.ts` 업데이트
3. `api.ts` 요청·응답 타입 확인
4. 액션 UI 그룹 분리
5. Drop/Break 결과 표시
6. Throw 애니메이션
7. NPC 쓰러짐 상태와 상호작용 차단
8. 브라우저 회귀 테스트

## 14. 테스트 계획

### 14.1 백엔드 테스트

- 물건을 들고 다른 위치로 이동해도 Drop/Break 액션이 존재
- Drop/Break의 `scope=held_item`, `target_id=null`
- 현재 위치 NPC별 Throw 액션 생성
- 쓰러진 NPC는 Throw 대상에서 제외
- 들고 있지 않은 물건 Throw 요청 차단
- 다른 위치 NPC Throw 요청 차단
- Throw 성공 후 물건 `destroyed`
- Throw 성공 후 player inventory 비어 있음
- Throw 성공 후 대상 NPC `is_fallen=true`
- 대상/소유자/목격자 감정과 관계 변화 검증
- 새 세션에서 NPC가 정상 상태로 초기화

### 14.2 Unity 액션 UI 테스트

- Backend keyboard를 든 채 PM에게 이동
- PM 물건 액션 표시
- 별도 소지품 섹션에 Drop/Break 표시
- PM 대상 Throw 액션 표시
- Backend 소유자에게 돌아가지 않아도 소지품 액션 유지

### 14.3 Drop 테스트

- PM 위치에서 Backend keyboard Drop
- 손 키보드 제거
- PM Drop Anchor에 키보드 표시
- Backend 책상에서는 키보드가 나타나지 않음
- 다시 집을 수 있음

### 14.4 Throw 애니메이션 테스트

- 플레이어 손에서 키보드 생성
- PM 방향으로 직선 이동
- 비행 중 지속 회전
- PM 외 다른 Collider 무시
- PM 충돌 시 키보드 두 조각 효과
- 투사체 제거

### 14.5 NPC 쓰러짐 테스트

- PM Sprite만 옆으로 회전
- 역할명·감정 라벨은 눕지 않음
- PM Collider 비활성화
- PM 상호작용 메뉴가 더 이상 나타나지 않음
- 대화 요청 차단
- 세션 동안 복구되지 않음

### 14.6 웹 프론트엔드 테스트

- Backend 응답의 `owner_id`, `scope`, `is_fallen` 파싱
- 현재 NPC 액션과 소지품 액션이 별도 그룹으로 표시
- 다른 NPC 위치에서도 Drop/Break가 유지
- 선택한 NPC에게 Throw 액션 표시
- Throw 성공 전에는 애니메이션을 시작하지 않음
- Throw 성공 후 키보드 회전 이동과 파괴 효과 확인
- 대상 NPC가 옆으로 누운 상태 유지
- 쓰러진 NPC의 대화·액션 버튼 비활성화
- 페이지 상태 갱신 후에도 쓰러짐 상태 유지
- 세션 reset 시 정상 상태 복구
- 데스크톱과 좁은 화면에서 액션 UI가 잘리지 않음

## 15. 완료 기준

- 플레이어 소지품 Drop/Break가 현재 NPC와 무관하게 액션창에 표시된다.
- 현재 NPC에게 Throw 액션이 표시된다.
- 투척 키보드가 회전하며 대상에게 이동한다.
- 충돌 시 키보드 파괴 효과가 실행된다.
- 대상 NPC가 옆으로 누운 상태로 유지된다.
- 쓰러진 NPC에게 대화·액션을 시도할 수 없다.
- 서버 snapshot과 Unity 인벤토리·월드 표현이 일치한다.
- PM 위치 Drop 시 Backend 책상으로 키보드가 되돌아가지 않는다.
- 기존 Pick up, Inspect, 대화, 감정 상태 기능이 회귀하지 않는다.
- 백엔드 전체 테스트와 Unity Play Mode 검증이 통과한다.
- 웹에서 현재 NPC 액션과 플레이어 소지품 액션이 구분된다.
- 웹 Throw 애니메이션이 서버 성공 응답 이후 실행된다.
- 웹 NPC 쓰러짐 표시와 상호작용 차단이 서버 `is_fallen` 상태와 일치한다.
- 웹 페이지를 다시 렌더링해도 snapshot 기준 상태가 유지된다.
- 웹 브라우저 회귀 테스트가 통과한다.

## 16. 예상 수정 파일

### Backend

- `backend/app/models.py`
- `backend/app/game/action_registry.py`
- `backend/app/game/engine.py`
- `backend/app/game/relationship_policy.py`
- `backend/tests/test_game_actions.py`
- `backend/tests/test_relationship_policy.py`

### Unity

- `Assets/OfficeMVP/Scripts/Backend/OfficeBackendClient.cs`
- `Assets/OfficeMVP/Scripts/Interaction/OfficeInteractionUI.cs`
- `Assets/OfficeMVP/Scripts/Interaction/InteractablePoint.cs`
- `Assets/OfficeMVP/Scripts/Interaction/PlayerInteractionDetector.cs`
- `Assets/OfficeMVP/Scripts/World/OfficeWorldObjectView.cs`
- `Assets/OfficeMVP/Scripts/World/OfficeWorldObjectStatePresenter.cs`
- `Assets/OfficeMVP/Scripts/World/OfficeBreakEffect.cs`
- 신규 `Assets/OfficeMVP/Scripts/World/OfficeThrownObjectProjectile.cs`
- 신규 `Assets/OfficeMVP/Scripts/World/OfficeThrowCoordinator.cs`
- 신규 `Assets/OfficeMVP/Scripts/Interaction/OfficeNpcFallView.cs`
- 필요 시 `Assets/OfficeMVP/Scripts/Core/OfficeMvpBootstrap.cs`

### Web Frontend

- `frontend/src/types.ts`
- `frontend/src/api.ts`
- `frontend/src/App.tsx`
- `frontend/src/styles.css`
- 필요 시 Frontend 테스트 파일
