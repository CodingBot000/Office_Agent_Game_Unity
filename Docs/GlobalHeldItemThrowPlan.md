# 전체 NPC 대상 소지 물건 투척 및 플레이어 액션 메뉴 개발계획서

## 1. 목적

플레이어가 물건을 든 상태에서 NPC 가까이 가지 않아도, 플레이어 머리 위의 `액션` 메뉴를 통해 물건과 대상 NPC를 선택해 던질 수 있게 한다.

기존의 근접 NPC 상호작용은 제거하지 않는다. 물건을 든 상태에서 특정 NPC에게 가까이 가면, 현재처럼 해당 NPC에게 바로 던지는 액션도 계속 표시한다.

대상 범위는 맵 전체의 쓰러지지 않은 NPC다. 구역이나 플레이어와의 거리로 제한하지 않는다.

## 2. 확정 UX

```text
플레이어가 Backend keyboard 소지
        |
        +-- NPC 근접 메뉴
        |     └─ Backend keyboard을(를) {가까운 NPC}에게 던지기
        |
        +-- 플레이어 머리 위 액션 버튼
              └─ 소지 물건 선택
                    └─ 행동 선택: 던지기
                          └─ 전체 NPC 대상 선택
                                └─ 투사체가 대상 NPC까지 이동
                                      └─ 파손 + 대상 쓰러짐
```

### 2.1 플레이어 액션 메뉴

- 버튼 위치: 플레이어 스프라이트 머리 바로 위.
- 구현 방식: 월드 오브젝트 버튼이 아니라 Canvas의 화면 UI를 `Camera.WorldToScreenPoint`로 플레이어 위치에 추적한다.
- 표시 조건: 소지품이 하나 이상이고, 전역 액션 UI·대화 입력창이 열려 있지 않을 때.
- 1단계: 소지한 물건 목록을 표시한다.
- 2단계: 선택한 물건에 가능한 행동을 표시한다. 이번 범위에서는 `던지기`만 제공한다.
- 3단계: 대상 선택 목록을 표시한다.
  - 이름, 역할, 현재 구역, 감정 상태를 표시한다.
  - `is_fallen=true` NPC는 목록에서 제외한다.
  - 취소/뒤로 버튼을 모든 단계에 둔다.

### 2.2 근접 NPC 메뉴 유지

- 기존 `대화하기`, `액션취하기` 메뉴를 유지한다.
- 플레이어가 물건을 들고 있으면, 현재 NPC가 대상인 `throw_{object}_at_{npc}` 액션을 기존 액션 목록에서 계속 노출한다.
- 전역 메뉴와 근접 메뉴는 동일한 서버 액션 ID와 같은 투척 연출 코디네이터를 사용한다.

## 3. 백엔드 변경

### 3.1 전체 대상 Throw 액션 생성

대상 파일:

- `backend/app/game/action_registry.py`

현재는 플레이어 `current_location`에 있는 NPC에게만 Throw 액션을 생성한다. 이를 다음으로 변경한다.

```text
플레이어가 정상 상태의 소지 물건을 하나 들고 있으면
  → 모든 is_fallen=false NPC에 대해 throw_{object_id}_at_{npc_id} 생성
```

액션 계약은 유지한다.

```text
family    = throw_held_object
scope     = target
object_id = backend_keyboard
target_id = pm_01
owner_id  = backend_01
location  = 플레이어의 현재 위치 (발사 위치)
```

`target_id`가 어떤 구역의 NPC인지 별도 필드로 중복 저장하지 않는다. NPC의 위치는 `NPC_HOME_LOCATIONS` 또는 NPC 위치 조회 함수로 계산한다.

### 3.2 서버 검증 변경

대상 파일:

- `backend/app/game/engine.py`

제거할 조건:

- 대상 NPC가 플레이어의 현재 구역에 있어야 한다는 조건.

유지할 조건:

- 물건 `holder_id == player`
- 물건 상태가 `destroyed`가 아님
- 대상 NPC 존재
- 대상 NPC `is_fallen == false`
- 플레이어 자신은 대상이 아님

성공 처리와 기존 상태 계약은 유지한다.

- 물건 소지 해제
- 물건 파괴 상태 전환
- 대상 NPC `is_fallen=true`
- 대상 NPC 대화 거부 처리
- 인벤토리 갱신
- 게임 액션 trace에 `target_id` 기록

### 3.3 원격 투척의 사회 정책 위치

전체 NPC 투척에서는 발사 위치와 충돌 위치가 다를 수 있다. 따라서 사회 정책의 목격자 계산은 반드시 **대상 NPC의 구역**을 기준으로 한다.

- 직접 피해자: 선택한 대상 NPC
- 목격자: 대상 NPC와 같은 구역의, 쓰러지지 않은 NPC
- 원래 물건 소유자: 대상과 다르면 별도 `property_aggression` 영향 적용
- 플레이어 현재 구역의 NPC는 원격 투척의 목격자로 처리하지 않는다.

이 규칙을 위해 `_apply_throw_policy`와 목격자 계산에 `impact_location` 또는 대상 NPC ID 기반 위치 계산을 전달한다.

### 3.4 API 및 세션 호환성

기존 `/game-actions` endpoint와 `AvailableGameAction` DTO를 그대로 사용한다. 새 endpoint는 만들지 않는다.

기존 세션에도 다음 필드가 안전하게 유지되어야 한다.

- `AvailableGameAction.scope`
- `AvailableGameAction.owner_id`
- `NPCState.is_fallen`
- `WorldObjectState.is_dropped`

필요하면 세션 스키마 버전을 올리고 migration 기본값을 추가한다.

## 4. Unity 변경

### 4.1 플레이어 액션 오버레이

대상 파일(예상):

- `Assets/OfficeMVP/Scripts/Interaction/OfficePlayerActionUI.cs` 신규
- `Assets/OfficeMVP/Scripts/Core/OfficeMvpBootstrap.cs`

구현:

- Canvas에 `PlayerActionAnchor`와 작은 `액션` 버튼을 생성한다.
- 매 프레임 플레이어의 월드 위치를 화면 좌표로 변환해 버튼 위치를 갱신한다.
- 화면 바깥, 대화창 열림, 액션 요청 진행 중에는 숨긴다.
- 소지품이 없으면 숨긴다.
- 버튼 클릭 시 물건 → 행동 → 전체 NPC 대상 선택 패널을 연다.

### 4.2 서버 액션 기반 대상 선택

클라이언트가 임의의 Throw ID를 조합하지 않는다. 최신 snapshot의 아래 액션만 사용한다.

```csharp
action.family == "throw_held_object"
&& action.object_id == selectedObjectId
&& action.target_id == selectedTargetId
&& action.enabled
```

따라서 서버 정책·차단 상태가 바뀌어도 UI는 서버가 허용한 대상만 선택한다.

### 4.3 투척 연출 재사용

대상 파일:

- `Assets/OfficeMVP/Scripts/World/OfficeThrowCoordinator.cs`
- `Assets/OfficeMVP/Scripts/World/OfficeThrownObjectProjectile.cs`

전역 메뉴의 선택 완료 후에도 기존 순서를 사용한다.

```text
PrepareThrow(action)
  → SubmitGameAction(action.id)
  → 서버 성공: ConfirmThrow(action)
  → 투사체 회전/이동
  → 파손 효과 + 대상 쓰러짐
```

대상이 다른 구역에 있어도 월드 좌표를 직접 사용하므로 별도 경로 탐색은 필요 없다. 투사체는 충돌 판정이 아닌 목표 좌표 도달 시 효과를 재생한다.

### 4.4 실패 및 상태 변화 처리

- 서버 차단·통신 실패: `CancelThrow(action)`, 투사체와 NPC 상태를 변경하지 않음.
- 대상이 요청 중 다른 이유로 쓰러짐: 서버 응답을 우선하고 선택 패널을 닫은 뒤 오류 메시지 표시.
- 대상 목록을 연 뒤 snapshot이 갱신되면, 쓰러진 NPC와 비활성 액션을 즉시 목록에서 제거.
- 기존 근접 액션 UI도 전역 대상 Throw 액션을 현재 NPC 기준으로 계속 필터링한다.

## 5. 웹 프론트엔드 변경

### 5.1 텍스트 대화 모드

대상 파일:

- `frontend/src/App.tsx`
- `frontend/src/types.ts`

`GAME ACTIONS`에 새 그룹을 추가한다.

```text
CURRENT LOCATION
  - 기존 Pick up / Inspect / 근접 대상 Throw

HELD ITEM
  - Drop / Break

GLOBAL THROW TARGETS
  - Backend keyboard → Backend Developer
  - Backend keyboard → Frontend Developer
  - Backend keyboard → QA Engineer
  - Backend keyboard → PM / Planner
```

여러 물건을 지원할 수 있도록 물건별로 묶고, 쓰러진 NPC의 액션은 표시하지 않는다.

### 5.2 2D 게임 모드

대상 파일:

- `frontend/src/VisualOffice.tsx`
- `frontend/src/styles.css`

추가 UX:

- 플레이어 머리 위에 `액션` 버튼을 렌더한다.
- 버튼 → 물건 → 대상 선택 패널을 맵 상단 중앙에 표시한다.
- 근접 NPC의 `액션 보기`에는 해당 NPC 대상 Throw를 계속 표시한다.
- 선택된 대상까지 CSS 투사체가 회전하며 이동한다.
- 파손 효과 후 `is_fallen` 상태의 NPC를 옆으로 눕히고 버튼을 비활성화한다.

웹은 Unity와 마찬가지로 snapshot의 액션 ID를 사용한다.

## 6. 테스트 계획

### 6.1 백엔드 단위 테스트

- Dev Area에서 Backend keyboard를 든 뒤 PM/QA 대상 Throw가 동시에 생성되는지 확인
- 대상 NPC가 다른 구역이어도 Throw 성공하는지 확인
- 쓰러진 NPC에는 Throw 액션이 생성되지 않는지 확인
- 원격 투척 목격자가 대상 구역 NPC로 계산되는지 확인
- 대상과 소유자가 동일/다른 경우 감정·관계 효과가 중복되지 않는지 확인
- 세션 저장/복원 후 `is_fallen`, 파괴 물건, trace `target_id`가 유지되는지 확인

### 6.2 Unity Play Mode 검증

- 소지품 없음: 플레이어 `액션` 버튼 숨김
- 소지품 있음: 버튼 표시 및 대상 선택 단계 전환
- 다른 구역 NPC 선택: 서버 성공 후 장거리 투사체·파손·쓰러짐 확인
- 쓰러진 NPC는 대상 목록과 근접 상호작용에서 제외되는지 확인
- 서버 실패 시 투사체·쓰러짐이 발생하지 않는지 확인
- 근접 NPC 메뉴의 바로 던지기 동작이 유지되는지 확인

### 6.3 웹 검증

- 텍스트 모드에서 전체 대상 Throw 그룹과 응답 메시지 확인
- 2D 모드에서 플레이어 액션 메뉴 → 대상 선택 → 투척 애니메이션 확인
- 근접 메뉴의 직접 Throw 유지 확인
- 데스크톱과 모바일에서 대상 선택 패널이 화면 안에 표시되는지 확인
- 브라우저 콘솔 오류 없이 `npm run build` 통과 확인

## 7. 구현 순서

```text
1. Backend 전체 대상 액션 생성 및 원격 목격자 정책 수정
2. Backend 테스트 및 API 계약 확인
3. Unity 플레이어 액션 오버레이와 전체 대상 선택 구현
4. Unity Play Mode에서 투척·실패·근접 메뉴 회귀 검증
5. Web 텍스트 모드 액션 그룹 반영
6. Web 2D 플레이어 액션 UI와 장거리 투척 연출 반영
7. Backend 전체 테스트 + Unity 컴파일/Play Mode + Web build/브라우저 검증
8. 각각의 저장소에 변경사항 커밋·푸시
```

## 8. 완료 기준

- 플레이어가 물건을 든 상태에서 어느 구역에 있든, 모든 정상 NPC를 전역 대상 선택 UI에서 고를 수 있다.
- 근접 NPC 메뉴의 직접 투척은 기존처럼 유지된다.
- 서버가 유일한 액션 권한 판단 주체이며, 클라이언트는 서버가 제공한 액션 ID만 호출한다.
- 원격 투척의 파손·쓰러짐·사회적 영향·저장 상태가 일관되게 반영된다.
- Unity와 웹 두 모드 모두에서 동일한 결과를 보여 준다.
