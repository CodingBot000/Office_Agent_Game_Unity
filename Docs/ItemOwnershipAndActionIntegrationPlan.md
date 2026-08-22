# 물건 보유 현황 및 사용 가능 액션 연동 개발계획서

## 1. 목적

백엔드가 관리하는 사무실 물건 상태를 Unity 게임에서 동일하게 표시하고, 플레이어가 다음 정보를 확인할 수 있도록 한다.

- Backend / Frontend / QA / PM이 기본적으로 보유한 물건
- 플레이어가 현재 들고 있는 물건
- 물건의 현재 위치, 소유자, 소지자, 상태
- 현재 위치와 게임 상태에 따라 서버가 허용한 사용 가능 액션

핵심 원칙은 **물건 상태와 액션 가능 여부의 권한을 서버가 갖고, Unity는 서버 스냅샷을 표시하고 요청하는 클라이언트로 동작하는 것**이다.

## 2. 현재 상태와 확인된 범위

### 백엔드에 이미 있는 데이터

백엔드 `GameSnapshot`에는 다음 필드가 이미 존재한다.

- `world_objects`
- `available_game_actions`
- `player_inventory`

물건 상태에는 다음 정보가 있다.

| 필드 | 의미 |
|---|---|
| `id` | 물건의 고유 ID |
| `name` | 표시 이름 |
| `owner_id` | 원래 소유한 NPC |
| `location` | 현재 월드 위치 |
| `holder_id` | 현재 들고 있는 주체. 없으면 바닥/책상 위 |
| `condition` | `normal`, `damaged`, `destroyed` |
| `portable` | 이동 가능한 물건인지 여부 |
| `destructible` | 파괴 가능한 물건인지 여부 |
| `evidence_id` | 증거 데이터와 연결되는 경우의 ID |

현재 시드 물건은 다음과 같다.

- `backend_keyboard`
- `frontend_keyboard`
- `qa_keyboard`
- `pm_keyboard`
- `meeting_room_monitor`
- `release_document`
- `qa_warning_printout`

### Unity 현재 상태

Unity의 `OfficeSnapshotDto`에는 현재 NPC와 `available_game_actions`만 정의되어 있다.

따라서 백엔드 응답 JSON에 `world_objects`와 `player_inventory`가 포함되어도 Unity 파서가 해당 필드를 보존하지 않아 게임 화면이나 로직에서 사용할 수 없다.

현재 액션 패널은 서버가 내려준 `available_game_actions`를 대상 NPC ID 기준으로 필터링해 표시한다. 물건 ID는 액션 DTO에 존재하지만 화면에는 충분히 노출되지 않고, 물건 상태와 인벤토리에는 연결되어 있지 않다.

## 3. 목표 데이터 흐름

```text
Backend session snapshot
        |
        +-- world_objects --------> Unity 물건 상태 저장소 ----> 맵 오브젝트 표시
        |
        +-- player_inventory ------> 보유 현황 UI
        |
        +-- available_game_actions -> 액션 패널
                                      |
                                      +--> 서버 game-actions 요청
                                                |
                                                +--> 갱신 snapshot 수신
                                                       |
                                                       +--> 물건/인벤토리/UI 갱신
```

대화, 이동, 게임 액션 응답으로 새로운 snapshot을 받으면 물건 상태와 보유 현황도 함께 갱신한다. Unity에서 물건을 임의로 획득·파괴·이동시키지 않는다.

## 4. 구현 계획

### 단계 1. Unity 데이터 DTO 확장

대상 파일:

- `Assets/OfficeMVP/Scripts/Backend/OfficeBackendClient.cs`

추가할 DTO:

```csharp
[Serializable]
public sealed class OfficeWorldObjectDto
{
    public string id;
    public string name;
    public string owner_id;
    public string location;
    public string evidence_id;
    public bool portable;
    public bool destructible;
    public string holder_id;
    public string condition;
}

[Serializable]
public sealed class OfficePlayerInventoryDto
{
    public string[] held_object_ids;
    public int max_held_objects;
}
```

`OfficeSnapshotDto`에 다음 필드를 추가한다.

```csharp
public OfficeWorldObjectDto[] world_objects;
public OfficePlayerInventoryDto player_inventory;
```

추가 규칙:

- 서버 필드명을 그대로 유지한다.
- `null` 배열을 빈 배열처럼 안전하게 처리한다.
- `condition`과 `holder_id`를 기준으로 표시 상태를 결정한다.
- 파싱 실패 시 기존 대화/이동 기능을 중단하지 않고 오류 상태만 표시한다.

### 단계 2. 서버 스냅샷 접근 API 정리

`OfficeBackendClient`에 화면과 다른 시스템이 안전하게 읽을 수 있는 API를 추가한다.

예시:

```csharp
public OfficeWorldObjectDto[] WorldObjects => CurrentSnapshot?.world_objects ?? Array.Empty<OfficeWorldObjectDto>();
public OfficePlayerInventoryDto PlayerInventory => CurrentSnapshot?.player_inventory;
```

필요하면 스냅샷 갱신 이벤트를 추가한다.

```csharp
public event Action<OfficeSnapshotDto> SnapshotUpdated;
```

`ApplySnapshot()`에서 다음 순서로 처리한다.

1. `CurrentSnapshot` 갱신
2. `SnapshotUpdated` 발생
3. 물건 표시, 보유 현황 UI, 액션 패널이 최신 데이터로 갱신

이벤트를 사용하면 대화·이동·게임 액션 각각에 중복 갱신 코드를 넣지 않아도 된다.

### 단계 3. 물건 ID와 Unity 맵 오브젝트 연결

현재 책상에 생성되는 키보드·모니터 오브젝트에 서버 물건 ID를 연결한다.

대상 파일:

- `Assets/OfficeMVP/Scripts/Core/OfficeMvpBootstrap.cs`
- 신규 `Assets/OfficeMVP/Scripts/World/OfficeWorldObjectView.cs`

각 오브젝트에 다음 정보를 둔다.

```csharp
public sealed class OfficeWorldObjectView : MonoBehaviour
{
    public string ObjectId;
    public SpriteRenderer Renderer;
}
```

초기 매핑 예시:

| Unity 맵 오브젝트 | 서버 `object_id` |
|---|---|
| Backend 책상 키보드 | `backend_keyboard` |
| Frontend 책상 키보드 | `frontend_keyboard` |
| QA 책상 키보드 | `qa_keyboard` |
| PM 책상 키보드 | `pm_keyboard` |
| 중앙 모니터 | `meeting_room_monitor` |

처음에는 서버에만 존재하고 맵에 아직 시각 에셋이 없는 문서·출력물은 보유 현황 UI에서 먼저 표시한다. 이후 별도 스프라이트를 추가해 월드 오브젝트로 확장한다.

### 단계 4. 물건 상태 표시 규칙

`OfficeWorldObjectView` 또는 별도 `OfficeWorldObjectPresenter`가 서버 상태를 받아 시각 상태를 갱신한다.

| 서버 상태 | MVP 표시 |
|---|---|
| `condition=normal`, `holder_id=null` | 기본 스프라이트 |
| `condition=normal`, `holder_id=player` | 플레이어 보유 아이콘 또는 월드 오브젝트 숨김 |
| `condition=normal`, `holder_id=npc_id` | 해당 NPC 보유 상태로 표시 |
| `condition=damaged` | 어두운 색/깨진 상태 표시 |
| `condition=destroyed` | 숨김 또는 파괴 표시 |

`owner_id`와 `holder_id`는 구분한다.

- `owner_id`: 원래 누구의 물건인지
- `holder_id`: 현재 누가 들고 있는지

### 단계 5. 게임 내 보유 현황 UI

신규 UI를 `OfficeInteractionUI`와 분리해 구현한다.

권장 신규 파일:

- `Assets/OfficeMVP/Scripts/Inventory/OfficeInventoryUI.cs`
- `Assets/OfficeMVP/Scripts/Inventory/OfficeInventoryPresenter.cs`

#### MVP 표시 방식

- 화면 우측 상단에 `보유 현황` 버튼 추가
- 버튼 클릭 또는 `I` 키로 패널 열기/닫기
- 패널은 반투명 UI로 구성
- Unity Canvas 기준 해상도 `1280×720` 유지

패널 구성:

```text
보유 현황

플레이어
  - 없음 / 물건명

Backend Developer
  - Backend keyboard

Frontend Developer
  - Frontend keyboard

QA Engineer
  - QA keyboard
  - QA warning printout

PM / Planner
  - PM keyboard
  - Release document

공용
  - Meeting room monitor
```

표시 규칙:

- `owner_id` 기준으로 기본 소유자를 그룹화한다.
- `holder_id`가 있으면 `현재 소지: 이름`을 함께 표시한다.
- 플레이어가 들고 있으면 플레이어 그룹에 표시하고 원래 소유자 행에는 `플레이어가 소지 중`을 표시한다.
- 파괴된 물건은 `파괴됨` 상태로 표시한다.
- 상태가 변경된 직후에는 해당 행을 짧게 강조 표시한다.
- 목록이 길어질 수 있으므로 패널 내부는 스크롤 가능하게 한다.

### 단계 6. 사용 가능 액션과 물건 상태 연동

현재 서버가 내려주는 `available_game_actions`를 단일 기준으로 사용한다.

액션 패널에는 다음 정보를 표시한다.

- 액션 이름: `label`
- 관련 물건 이름: `object_id`를 `world_objects`에서 찾아 표시
- 대상 NPC: `target_id`가 있으면 표시
- 위치: `location`
- 비활성 사유: `disabled_reason`

예시:

```text
QA Engineer에게 액션취하기

[QA keyboard] 검사하기
[QA keyboard] 집기
[QA warning printout] 조사하기
```

동작 규칙:

1. 액션 패널을 열 때 최신 snapshot의 `available_game_actions`를 사용한다.
2. `object_id`가 있으면 물건 상태와 연결해 버튼에 물건명을 표시한다.
3. `enabled=false`인 액션은 비활성화하고 `disabled_reason`을 표시한다.
4. 버튼 클릭 시 `action_id`만 서버로 전송한다.
5. 응답의 `snapshot`을 다시 적용한다.
6. 보유 현황·월드 오브젝트·액션 목록을 동시에 갱신한다.
7. 서버에 없는 액션은 Unity에서 새로 만들지 않는다.

### 단계 7. 플레이어 인벤토리 액션 처리

백엔드에 이미 정의된 액션 패밀리를 Unity 버튼과 연결한다.

- `pick_up_object`
- `drop_held_object`
- `break_held_object`
- `throw_held_object`
- `inspect_object`

MVP에서는 플레이어가 물건을 여러 개 들지 않도록 서버의 `max_held_objects`를 기준으로 처리한다. 현재 백엔드 기본값은 1개이므로 Unity에서 별도 수량 규칙을 만들지 않는다.

## 5. 서버 변경 계획

현재 백엔드 모델과 응답 구조만으로 기본 연동이 가능하므로, 1차 구현에서는 API 경로를 변경하지 않는다.

사용 API:

- `POST /api/v1/sessions`
- `GET /api/v1/sessions/{session_id}`
- `POST /api/v1/sessions/{session_id}/actions`
- `POST /api/v1/sessions/{session_id}/game-actions`

각 응답의 `snapshot`에 다음 필드가 계속 포함되어야 한다.

- `world_objects`
- `player_inventory`
- `available_game_actions`

추후 필요한 경우에만 다음을 추가한다.

- 물건 상세 조회 전용 API
- 물건 변경 이벤트 스트리밍
- 물건 이미지/프리팹 메타데이터 필드

## 6. 테스트 계획

### 백엔드 계약 테스트

- 세션 생성 응답에 `world_objects`가 포함되는지 확인
- `player_inventory`가 포함되는지 확인
- 각 물건의 `owner_id`와 `location`이 올바른지 확인
- 물건 집기 후 `holder_id`와 `player_inventory.held_object_ids`가 갱신되는지 확인
- 물건 내려놓기 후 `holder_id`와 인벤토리가 갱신되는지 확인
- 파괴 후 `condition=destroyed`가 되는지 확인
- 상태 변경 후 `available_game_actions`가 갱신되는지 확인

### Unity 단위/런타임 테스트

- 서버 JSON이 Unity DTO로 정상 파싱되는지 확인
- `world_objects`의 7개 항목이 누락 없이 저장되는지 확인
- owner/holder 표시가 구분되는지 확인
- 플레이어 인벤토리의 빈 상태와 보유 상태를 확인
- `object_id`가 있는 액션에 물건명이 표시되는지 확인
- `enabled=false` 액션이 실행되지 않는지 확인
- 게임 액션 응답 후 보유 현황과 액션 목록이 함께 갱신되는지 확인

### 실제 MVP 시나리오

1. 게임 시작
2. `보유 현황` 패널에서 7개 물건 확인
3. QA 위치로 이동
4. QA keyboard 관련 액션 확인
5. keyboard 집기
6. 보유 현황에서 플레이어가 keyboard를 소지한 상태 확인
7. QA의 물건 행에 `플레이어가 소지 중` 표시 확인
8. QA keyboard 내려놓기
9. 다시 QA 위치의 액션 목록과 보유 현황 갱신 확인
10. 파괴 가능한 물건을 파괴하고 `destroyed` 표시 확인

## 7. 완료 기준

- 서버 스냅샷의 `world_objects`와 `player_inventory`가 Unity에서 파싱된다.
- 게임 내 보유 현황 패널에서 NPC별 물건과 플레이어 보유 물건을 확인할 수 있다.
- owner와 holder가 혼동되지 않는다.
- 액션 패널에 관련 물건 이름과 현재 상태가 표시된다.
- 사용 가능/불가능 여부는 서버의 `available_game_actions`와 일치한다.
- 물건을 집거나 내려놓거나 파괴한 뒤 UI와 맵이 서버 snapshot 기준으로 갱신된다.
- Unity에서 임의로 허용한 액션이나 물건 상태가 서버 상태와 불일치하지 않는다.
- 기존 이동, 충돌, 대화, 엔터 전송 기능이 회귀하지 않는다.
- `1280×720 / 16:9` 화면에서 보유 현황과 액션 패널이 잘리지 않는다.

## 8. 작업 순서 및 산출물

| 순서 | 작업 | 산출물 |
|---:|---|---|
| 1 | DTO 및 snapshot 보존 확장 | `OfficeBackendClient.cs` 수정 |
| 2 | snapshot 갱신 이벤트/상태 저장소 | Unity 런타임 상태 연결 |
| 3 | 맵 오브젝트 ID 매핑 | `OfficeWorldObjectView.cs` |
| 4 | 물건 상태 Presenter | 월드 물건 상태 반영 |
| 5 | 보유 현황 UI | `OfficeInventoryUI.cs` |
| 6 | 액션 패널 물건 정보 보강 | `OfficeInteractionUI.cs` 수정 |
| 7 | 통합 테스트 및 화면 검증 | 테스트 로그와 1280×720 캡처 |

## 9. 제외 범위

1차 MVP에서는 다음을 제외한다.

- 아이템 드래그 앤 드롭 인벤토리
- 아이템 수량 스택
- 아이템 장착 슬롯
- 캐릭터별 별도 인벤토리 창 애니메이션
- 실시간 멀티플레이어 이벤트 스트리밍
- 물건별 고해상도 전용 스프라이트 제작

먼저 서버 스냅샷을 정확히 Unity에 연결하고, 보유 현황과 액션 가능 여부가 항상 같은 데이터에서 갱신되는 구조를 완성한다.
