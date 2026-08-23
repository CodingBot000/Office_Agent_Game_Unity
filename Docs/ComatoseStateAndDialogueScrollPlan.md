# 혼수상태 NPC 상태·물건 상호작용·대화 스크롤 개발계획서

## 1. 목적

투척에 맞은 NPC를 단순한 `쓰러짐/상호작용 불가` 상태가 아니라, 명시적인 `혼수상태(comatose)`로 관리한다.

혼수상태 NPC는 대답할 수 없지만, 플레이어는 NPC 가까이 가서 상호작용 메뉴와 액션 메뉴를 열고 그 NPC의 물건을 조사하거나 집을 수 있다. 또한 모든 NPC가 해당 폭력 사건과 혼수상태 발생 사실을 지속적으로 인지하도록 한다.

동시에 Unity와 웹의 대화 로그가 길어져도 스크롤 가능하고, 새 메시지 추가 시 항상 최신 메시지가 보이도록 하단으로 자동 이동하게 한다.

## 2. 확정 정책

| 항목 | 혼수상태 NPC 처리 |
|---|---|
| 상태값 | `comatose` |
| 외형 | 옆으로 쓰러진 스프라이트 유지 |
| 물리 충돌 | 유지 |
| 근접 감지 | 유지 |
| `대화하기` 메뉴 | 표시 및 선택 가능 |
| 대화 요청 | `혼수상태로 답변할 수 없습니다.` 즉시 반환 |
| `액션취하기` 메뉴 | 표시 및 선택 가능 |
| 해당 NPC 물건 | 조사·줍기 등 기존 물건 액션 가능 |
| 새 Throw 대상 | 제외 |
| 회복 | 이번 범위에서는 없음 |

## 3. 백엔드 구현 계획

### 3.1 NPC 상태 모델

대상 파일:

- `backend/app/models.py`
- `backend/app/game/engine.py`

`NPCState`에 상태 enum을 추가한다.

```python
NpcPhysicalState = Literal["normal", "comatose"]

class NPCState(BaseModel):
    physical_state: NpcPhysicalState = "normal"
```

기존 `is_fallen`은 상태 중복을 피하기 위해 단계적으로 제거하거나, 마이그레이션 기간에는 `physical_state == "comatose"`에서 파생되는 호환 필드로만 유지한다. 새 구현의 권한 판단은 반드시 `physical_state` 하나만 사용한다.

세션 스키마 버전을 올리고, 기존 저장 세션은 다음처럼 변환한다.

```text
is_fallen=true  → physical_state=comatose
그 외           → physical_state=normal
```

### 3.2 투척 성공 처리

`throw_held_object` 성공 시:

```text
대상 physical_state = comatose
대상은 새 Throw 액션 목록에서 제외
물건 holder_id = null
물건 condition = destroyed
인벤토리 갱신
```

기존 `dialogue_refused_npc_ids`에는 혼수상태만을 이유로 추가하지 않는다. 이 목록은 사과·복구·중재가 필요한 별도 사회적 대화 거부 정책에만 남긴다.

### 3.3 혼수상태 대화 응답

대상 파일:

- `backend/app/game/engine.py`

`_talk_npc`, `_ask_npc`, `_accuse_npc`, `_defend_npc` 진입부에서 일반 대화 거부보다 먼저 `physical_state == "comatose"`를 검사한다.

반환 규칙:

```text
{NPC 이름}은(는) 혼수상태로 답변할 수 없습니다.
```

- LLM/CLI 의사결정 호출은 하지 않는다.
- 요청은 서버 오류나 액션 차단이 아닌 정상 응답으로 처리한다.
- 플레이어 입력과 혼수상태 정책 응답을 이벤트 로그에 남긴다.
- 턴 소모 여부는 기존 대화와 동일하게 소모한다.

### 3.4 물건 상호작용 유지

혼수상태는 NPC 신체 상태이며 물건 소유권이나 물건 위치를 잠그지 않는다.

- `inspect_*`, `pick_up_*` 생성 조건에 NPC 혼수상태를 추가하지 않는다.
- 혼수상태 NPC의 키보드·문서 등은 현재 위치 규칙에 따라 계속 노출한다.
- Drop/Break 등 플레이어 소지품 액션도 그대로 유지한다.

### 3.5 모든 NPC의 사건 인지

현재 목격자 정책은 사회적 효과를 대상 구역에 한정한다. 이는 감정·관계 변화에 적절하지만, “사건을 알고 있음”과는 분리해야 한다.

투척으로 혼수상태가 발생하면 모든 NPC에 다음 중요 기억을 추가한다.

```text
{대상 NPC}이(가) Player가 던진 {물건}에 맞아 혼수상태에 빠졌다.
```

규칙:

- 대상·원래 물건 소유자·목격자는 기존의 강한 사회 정책 효과를 유지한다.
- 그 외 NPC는 감정·관계 수치를 강제로 변경하지 않고 사건 기억만 추가한다.
- 중복 투척이나 세션 복원 시 같은 사건을 중복 기억으로 넣지 않는다.
- 이후 각 NPC 대화의 LLM 컨텍스트에 이 기억이 포함되어 사건을 인지한 답변을 생성할 수 있어야 한다.

구현은 기존 `important_memories`와 사건별 고유 키(투척 trace ID 또는 turn+target+object)를 이용한다.

## 4. Unity 구현 계획

### 4.1 DTO 및 월드 상태

대상 파일:

- `Assets/OfficeMVP/Scripts/Backend/OfficeBackendClient.cs`
- `Assets/OfficeMVP/Scripts/World/OfficeWorldObjectStatePresenter.cs`

`OfficeNpcDto`에 `physical_state`를 추가하고, 월드 프레젠터는 이 값이 `comatose`일 때 쓰러짐 연출을 적용한다.

### 4.2 쓰러짐 뷰의 상호작용 보존

대상 파일:

- `Assets/OfficeMVP/Scripts/Interaction/OfficeNpcFallView.cs`
- `Assets/OfficeMVP/Scripts/Interaction/PlayerInteractionDetector.cs`

현재 쓰러짐 뷰는 다음을 끈다.

```text
SpriteRenderer 원본
Collider2D 전부
InteractablePoint
```

수정 후:

- 원본 SpriteRenderer만 숨기고 `FallenVisual`을 표시한다.
- Solid Collider와 trigger Collider를 활성 상태로 유지한다.
- `InteractablePoint`를 활성 상태로 유지한다.
- 플레이어 감지기에서 혼수상태 NPC를 건너뛰지 않는다.

### 4.3 UI 표시

대상 파일:

- `Assets/OfficeMVP/Scripts/Interaction/OfficeCharacterEmotionLabel.cs`
- `Assets/OfficeMVP/Scripts/Interaction/OfficeInteractionUI.cs`
- `Assets/OfficeMVP/Scripts/Interaction/OfficePlayerActionUI.cs`

- 역할/감정 표시 근처에 `혼수상태` 배지를 표시한다.
- 근접 메뉴의 `대화하기`, `액션취하기` 버튼은 계속 표시한다.
- 대화 응답이 오면 기존 대화 로그에 혼수상태 문구를 일반 응답으로 기록한다.
- 전역 Throw 대상 목록과 근접 Throw 액션에서는 혼수상태 NPC를 제외한다.

### 4.4 Unity 대화 스크롤

대상 파일:

- `Assets/OfficeMVP/Scripts/Interaction/OfficeInteractionUI.cs`

현재 문제:

- Text에는 `ContentSizeFitter`가 있지만 ScrollRect Content의 실제 높이가 텍스트 높이에 맞춰지지 않을 수 있다.
- 같은 프레임에 `verticalNormalizedPosition=0`을 설정하면 레이아웃 갱신 전이라 마지막 줄이 보이지 않을 수 있다.

수정:

1. 대화 Content `RectTransform`을 필드로 보관한다.
2. Text preferred height로 Content 높이를 설정하거나 `VerticalLayoutGroup + ContentSizeFitter`를 Content에 부착한다.
3. 메시지 추가 후 `Canvas.ForceUpdateCanvases()`와 `LayoutRebuilder.ForceRebuildLayoutImmediate()`를 수행한다.
4. 다음 프레임 코루틴에서 `dialogueScroll.verticalNormalizedPosition = 0f`를 설정한다.
5. NPC 탭을 전환해도 해당 탭의 마지막 메시지로 자동 이동한다.

## 5. 웹 프론트엔드 구현 계획

### 5.1 타입 및 상태 표현

대상 파일:

- `frontend/src/types.ts`
- `frontend/src/VisualOffice.tsx`
- `frontend/src/App.tsx`

`NPCState.physical_state`를 추가하고, `is_fallen` 기반 표현을 `physical_state === "comatose"`로 전환한다.

웹 2D 모드에서는:

- NPC를 옆으로 눕힌다.
- 근접 감지·캐릭터 선택·대화/액션 메뉴를 비활성화하지 않는다.
- 팀 목록과 선택 상세에는 `COMATOSE / 혼수상태`를 표시한다.
- Throw 대상 선택과 직접 Throw 액션에서만 제외한다.

### 5.2 웹 텍스트 로그 자동 스크롤

대상 파일:

- `frontend/src/App.tsx`
- `frontend/src/styles.css`

`.event-log`는 이미 `overflow-y: auto`다. 다음을 추가한다.

```text
eventLogRef 생성
snapshot.events 길이 또는 pendingCommand 변경 시
  eventLogRef.current.scrollTop = eventLogRef.current.scrollHeight
```

새 플레이어 입력, 서버 NPC 응답, 혼수상태 정책 응답, 오류 상태 모두 마지막 항목이 보이게 한다.

## 6. 테스트 계획

### 6.1 백엔드

- 투척 후 대상 `physical_state=comatose` 확인
- 대상이 Throw 목록에서 제외되는지 확인
- 혼수상태 NPC에게 대화하면 LLM 호출 없이 혼수상태 문구가 반환되는지 확인
- 혼수상태 NPC의 키보드/문서 Inspect·Pick up 가능 확인
- 모든 NPC의 중요 기억에 투척·혼수상태 사건이 남는지 확인
- 기존 `dialogue_refused_npc_ids` 정책과 혼수상태 응답이 구분되는지 확인
- 세션 저장/복원 migration 확인

### 6.2 Unity Play Mode

- 투척 후 NPC가 쓰러지고 `혼수상태` 배지 표시 확인
- 쓰러진 NPC와 충돌·근접 감지·대화 메뉴·액션 메뉴가 유지되는지 확인
- 대화 입력 시 혼수상태 응답이 로그에 표시되는지 확인
- 해당 NPC의 물건 Inspect/Pick up 가능 확인
- 긴 대화 및 NPC 탭 전환 시 스크롤이 마지막 메시지로 내려가는지 확인

### 6.3 웹

- 텍스트 모드에서 혼수상태 NPC 대화 응답과 이벤트 로그 자동 하단 스크롤 확인
- 2D 모드에서 혼수상태 NPC를 선택·상호작용할 수 있는지 확인
- Throw 대상 목록에서는 해당 NPC가 사라지는지 확인
- 혼수상태 NPC 물건 액션이 계속 노출되는지 확인
- 데스크톱·모바일에서 긴 로그의 스크롤 및 최신 메시지 표시 확인

## 7. 구현 순서

```text
1. Backend physical_state 모델·migration·투척 처리 수정
2. 전체 NPC 사건 기억 및 혼수상태 대화 응답 구현
3. Backend 테스트
4. Unity DTO·쓰러짐 뷰·상호작용 유지·대화 스크롤 수정
5. Unity 컴파일 및 Play Mode 검증
6. Web 타입·혼수상태 UI·이벤트 로그 자동 스크롤 수정
7. Frontend build 및 브라우저 검증
8. 두 저장소 커밋·푸시
```

## 8. 완료 기준

- 투척 대상은 혼수상태로 표시되고 이후 Throw 대상에서 제외된다.
- 혼수상태 NPC와 대화 UI·액션 UI·물건 상호작용은 유지된다.
- 혼수상태 NPC는 대답 대신 일관된 혼수상태 안내 문구를 반환한다.
- 모든 NPC가 사건을 지속 기억해 후속 대화에서 인지한다.
- Unity와 웹의 긴 대화/이벤트 로그는 스크롤 가능하며 새 메시지마다 하단으로 이동한다.
