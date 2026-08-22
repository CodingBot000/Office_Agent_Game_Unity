# 시작 직후 액션 목록 초기화 및 갱신 수정 작업계획서

## 1. 문제 정의

게임을 시작한 직후 캐릭터에게 접근해 `액션취하기`를 누르면 액션이 없는 것처럼 보인다. 이후 이동, 다른 버튼 클릭, 액션창 재오픈 등으로 추가 동작을 하면 액션이 나타난다.

현재 확인된 현상은 다음과 같다.

```text
액션창을 즉시 열었을 때
  서버 current_location = meeting_room
  available_game_actions = 0
  Unity 액션 목록 = 없음

QA 위치 동기화가 완료된 뒤
  서버 current_location = qa_desk
  available_game_actions = 3
  QA 액션 = 정상적으로 생성됨
```

QA 물건 자체가 누락된 문제는 아니다. 서버는 초기 스냅샷에서 `world_objects` 전체와 `player_inventory`를 이미 보내고 있다. 다만 `available_game_actions`는 전체 액션 목록이 아니라 **서버가 인식하는 현재 위치에서 가능한 액션만** 생성한다.

## 2. 현재 구조의 원인

### 2.1 초기 스냅샷과 위치별 액션은 다른 데이터다

초기 세션 스냅샷에는 다음이 포함된다.

- NPC 상태
- 전체 `world_objects`
- `player_inventory`
- 현재 위치의 `available_game_actions`

게임 시작 위치는 `meeting_room`이고 QA/Backend/Frontend/PM의 물건은 해당 구역에 있으므로 초기 액션 수가 0인 것은 정상이다.

### 2.2 위치 동기화가 비동기다

`OfficeLocationZone`은 플레이어가 구역에 들어간 뒤 `SubmitMove()`를 호출한다. 서버 응답이 도착하기 전까지 `OfficeBackendClient.CurrentSnapshot`은 이전 위치의 스냅샷을 유지한다.

따라서 플레이어가 QA 캐릭터 근처에 도착해도 다음 순간이 발생할 수 있다.

```text
Unity 상호작용 대상 = qa_01
서버 스냅샷 위치 = meeting_room
```

### 2.3 액션창이 최신 스냅샷을 자동 반영하지 않는다

현재 `OfficeInteractionUI.OpenActions()`는 액션창을 여는 순간 `RebuildActionList()`를 한 번 호출한다. 이후 `SnapshotUpdated` 이벤트가 발생해도 액션창을 다시 그리지 않는다.

결과적으로 다음 순서가 된다.

```text
1. QA 근처 도착
2. 위치 이동 요청 전송
3. 서버 응답 전에 액션창 열기
4. 이전 meeting_room 스냅샷으로 '액션 없음' 표시
5. QA 위치 응답 도착
6. CurrentSnapshot만 갱신되고 열린 액션창은 그대로 유지
```

## 3. 수정 목표

- 시작 직후 전체 월드 물건과 플레이어 인벤토리는 현재처럼 즉시 수신한다.
- 위치가 바뀌면 서버 스냅샷과 액션 목록을 자동으로 갱신한다.
- 액션창을 너무 빨리 열어도 `액션 없음`으로 확정하지 않는다.
- 위치 동기화가 끝나면 액션 버튼이 자동으로 나타난다.
- 빠르게 구역을 이동해도 이전 위치 응답이 최신 위치를 덮어쓰지 않게 한다.
- 서버가 최종 액션 가능 여부를 결정한다.

## 4. 구현 계획

### 단계 1. 위치 동기화 상태 추가

대상 파일:

- `Assets/OfficeMVP/Scripts/Backend/OfficeBackendClient.cs`
- `Assets/OfficeMVP/Scripts/World/OfficeLocationZone.cs`

추가할 상태:

```csharp
public string PendingLocation { get; private set; }
public bool IsLocationSyncing { get; private set; }
public event Action<string> LocationSyncStarted;
public event Action<string> LocationSyncCompleted;
```

`SubmitMove(location, ...)` 실행 시:

1. `PendingLocation`을 최신 요청 위치로 설정
2. `IsLocationSyncing = true`
3. `LocationSyncStarted` 발생
4. 서버 응답 수신
5. 최신 요청과 응답 위치가 일치할 때만 `CurrentSnapshot` 적용
6. `IsLocationSyncing = false`
7. `LocationSyncCompleted` 발생

### 단계 2. 초기 세션 준비 후 현재 위치 재동기화

현재 위치 트리거가 Backend 세션 준비 전에 발생하면 `OfficeLocationZone`이 로그만 남기고 종료한다.

수정 방향:

- 세션 준비 전에는 `playerInside = true`를 확정하지 않는다.
- Backend 세션이 준비되면 플레이어의 실제 위치를 검사한다.
- 현재 플레이어가 들어가 있는 구역을 찾아 최초 위치 동기화를 다시 요청한다.
- 초기화 시 `meeting_room`, `dev_area`, `qa_desk`, `pm_desk`가 중복 요청되지 않도록 하나의 최신 위치만 전송한다.

### 단계 3. 액션창의 스냅샷 이벤트 구독

대상 파일:

- `Assets/OfficeMVP/Scripts/Interaction/OfficeInteractionUI.cs`

`Start()`에서 `OfficeBackendClient.SnapshotUpdated`를 구독한다.

```csharp
backend.SnapshotUpdated += OnSnapshotUpdated;
```

`OnSnapshotUpdated()` 동작:

1. 현재 액션창이 열려 있는지 확인
2. 현재 상호작용 대상이 있는지 확인
3. 위치 동기화가 끝났는지 확인
4. 조건을 만족하면 `RebuildActionList()` 실행

`OnDestroy()`에서 이벤트를 해제해 중복 구독을 방지한다.

### 단계 4. 액션창 로딩 상태 표시

액션창을 열었을 때 서버 위치가 대상 캐릭터 위치와 다르면 다음 문구를 표시한다.

```text
현재 위치의 액션을 확인하는 중...
```

이때 다음을 수행한다.

- 기존 `NoActions` 행을 표시하지 않는다.
- 액션 버튼을 생성하지 않는다.
- 위치 동기화 완료 이벤트를 기다린다.
- 최신 스냅샷이 도착하면 자동으로 액션 목록을 생성한다.

서버가 최종적으로 액션 0개를 반환한 경우에만 다음 문구를 표시한다.

```text
현재 이 위치에서 이 대상에게 가능한 액션이 없습니다.
```

### 단계 5. 대상 위치 매핑 명시화

현재 `InteractablePoint`는 대상 ID와 표시 이름만 가지고 있다. 액션창이 대상과 서버 위치를 비교할 수 있도록 위치 ID를 명시한다.

예시:

| 대상 ID | 위치 ID |
|---|---|
| `backend_01` | `dev_area` |
| `frontend_01` | `dev_area` |
| `qa_01` | `qa_desk` |
| `pm_01` | `pm_desk` |

추가 방식:

```csharp
interactable.Configure(targetId, displayName, locationId);
```

액션창을 열 때:

- 대상 위치와 서버 `current_location`이 같으면 즉시 액션 표시
- 다르면 위치 동기화 상태를 표시하고 최신 snapshot을 기다림

### 단계 6. 빠른 이동 시 최신 위치 보장

플레이어가 QA → PM → 회의실을 빠르게 이동할 때 오래된 응답이 최신 상태를 덮어쓰지 않도록 한다.

권장 방식:

- 이동 요청에 증가하는 요청 번호 부여
- 가장 최근 요청 번호만 적용
- 이전 요청 응답은 상태 적용 없이 무시
- 또는 이동 요청을 하나의 최신 위치 큐로 합쳐 마지막 위치만 전송

목표 상태:

```text
최종 플레이어 위치 = QA
최종 CurrentSnapshot.current_location = qa_desk
최종 available_game_actions = QA 위치 액션
```

## 5. 액션 목록 생성 정책

전체 액션을 시작 시 모두 내려받는 방식으로 변경하지 않는다.

이유:

- `available_game_actions`는 위치, 물건 상태, 플레이어 인벤토리에 따라 달라진다.
- 키보드를 집은 뒤에는 `drop`과 `break`가 생기고 `pick_up`은 사라진다.
- 파괴된 물건은 `inspect`와 `pick_up`에서 제외되어야 한다.
- 서버가 현재 상태에 맞는 액션만 계산하는 구조가 안전하다.

따라서 서버는 현재 위치 기준 액션만 반환하고, Unity는 위치 동기화 완료 후 최신 목록을 자동 반영한다.

## 6. 검증 계획

### 시작 직후

1. 세션 생성
2. `objects=7`, `held=0` 수신 확인
3. 플레이어를 QA 근처로 이동
4. 액션창을 즉시 열기
5. `액션을 확인하는 중...` 표시 확인
6. 위치 응답 후 QA 액션 3개 자동 표시 확인

### QA 액션

다음 액션이 액션창 재오픈 없이 표시되어야 한다.

- `pick_up_qa_keyboard`
- `inspect_qa_warning_printout`
- `pick_up_qa_warning_printout`

### 인벤토리 상태 변경

1. QA keyboard 집기
2. snapshot 갱신
3. 액션창에 `Drop QA keyboard`, `Break QA keyboard` 표시
4. `Pick up QA keyboard`는 사라짐

### 빠른 이동

- QA → PM → 회의실 빠른 이동
- 최종 위치와 서버 snapshot 위치 일치 확인
- 이전 위치 액션이 남지 않는지 확인

### 회귀 테스트

- 대화창 열기/전송/응답 포커스 유지
- 엔터 전송
- 보유 현황 상태/소유 물건 탭
- 캐릭터 감정 라벨 갱신
- 충돌 및 이동
- 원형 `i` 버튼과 정보창 슬라이드

## 7. 완료 기준

- 시작 직후 물건/인벤토리 데이터는 정상 수신된다.
- 액션창을 처음 열어도 서버 위치 동기화 전에는 빈 목록으로 확정되지 않는다.
- QA/Backend/Frontend/PM 위치에 도착하면 해당 액션 목록이 자동으로 표시된다.
- 위치 응답이 도착한 뒤 액션창을 닫았다 다시 열 필요가 없다.
- 서버 snapshot이 갱신될 때 열린 액션창도 자동 갱신된다.
- 집기/Drop/Break 이후 액션 목록이 즉시 상태에 맞게 바뀐다.
- 빠른 구역 이동 후에도 최신 위치의 액션만 표시된다.
- `1280×720 / 16:9` 화면에서 로딩 문구와 액션 버튼이 잘리지 않는다.

## 8. 예상 수정 파일

- `Assets/OfficeMVP/Scripts/Backend/OfficeBackendClient.cs`
- `Assets/OfficeMVP/Scripts/World/OfficeLocationZone.cs`
- `Assets/OfficeMVP/Scripts/Interaction/InteractablePoint.cs`
- `Assets/OfficeMVP/Scripts/Interaction/OfficeInteractionUI.cs`
- 필요 시 `Assets/OfficeMVP/Scripts/Core/OfficeMvpBootstrap.cs`
