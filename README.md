# Office Agent Unity

Unity로 구현한 2D 비주얼 오피스 게임 클라이언트입니다. 게임 세션과 대화, 액션, 월드 상태는 별도의 FastAPI 백엔드와 통신합니다.

## 요구사항

- Unity `6000.4.0f1`
- Python 3.11 이상
- 백엔드 실행을 위한 `uv`
- 백엔드 저장소: `Office_Agent_MVP/backend`

Unity 클라이언트만 실행할 때 프론트엔드 웹 앱은 필요하지 않습니다.

## 백엔드 실행

Unity를 실행하기 전에 백엔드를 먼저 시작합니다.

```bash
cd /path/to/Office_Agent_MVP/backend
cp .env.example .env
uv sync --extra test
uv run uvicorn app.main:app --host 127.0.0.1 --port 8000
```

로컬 테스트만 할 때는 백엔드 `.env`에서 `AI_PROVIDER=deterministic-mock`으로 설정하면 별도 provider 인증 없이 실행할 수 있습니다. 실제 AI provider를 사용하는 경우의 설정과 인증은 백엔드 저장소의 README를 참고합니다.

인증 정보는 백엔드의 로컬 환경변수로만 관리하며 Unity 프로젝트에 저장하지 않습니다.

백엔드가 정상적으로 실행되면 다음 주소가 응답합니다.

```text
http://127.0.0.1:8000/health
```

## Unity 실행

1. Unity Hub에서 이 프로젝트를 Unity `6000.4.0f1`로 엽니다.
2. `Assets/OfficeMVP/Scenes/OfficeMVP.unity` 씬을 엽니다.
3. Unity Editor의 Play 버튼을 누릅니다.
4. 시작 화면에서 백엔드 상태를 확인합니다.
5. 로컬 백엔드가 `ONLINE`이면 `LOCAL`을 선택합니다.

시작 화면에서는 로컬과 원격 백엔드의 health 상태를 함께 확인합니다. 연결 대상을 선택하기 전에는 플레이어 이동과 상호작용이 잠깁니다.

## 플레이 가이드

### 이동 및 상호작용

- `WASD` 또는 방향키: 플레이어 이동
- `E`: 가까운 캐릭터 또는 상호작용 대상 선택
- 화면에 표시되는 버튼: 대화와 액션 메뉴 열기

### 대화

1. 캐릭터 가까이 이동합니다.
2. `E`를 눌러 상호작용 메뉴를 엽니다.
3. `대화하기`를 선택합니다.
4. 대화창에 내용을 입력하고 `전송`을 누릅니다.

대화 기록은 캐릭터별로 유지되며, 현재 가까이 있는 캐릭터에게만 새 대화를 보낼 수 있습니다.

### 액션과 소지품

1. 캐릭터 가까이 이동합니다.
2. `E`를 누른 뒤 `액션취하기`를 선택합니다.
3. 가능한 액션 또는 소지품 액션을 선택합니다.
4. 소지품을 던지는 액션은 물건과 대상에 따라 결과와 시각 효과가 달라집니다.

플레이어 근처에 `액션` 버튼이 표시되면 소지품을 먼저 선택한 뒤 던질 대상을 선택할 수도 있습니다. 소지품 목록과 월드 상태는 백엔드 응답에 따라 갱신됩니다.

### 시나리오 요약

이미 배포 장애가 발생한 상태에서 게임이 시작됩니다. Backend Developer는 API 응답 스키마를 변경했고, Frontend에는 변경사항이 늦게 전달되었습니다. QA는 배포 전 API 불일치를 발견하고 배포 중단을 경고했지만, 일정 압박 속에서 배포가 진행되어 장애가 발생했습니다.

권장 진행 순서는 다음과 같습니다.

1. QA에게 장애 원인을 질문합니다.
2. 정확한 에러명이나 QA 경고 원문을 요청해 `QA warning message` 증거를 확보합니다.
3. Backend Developer에게 API 변경과 배포 판단 과정을 질문하고 QA 경고 증거를 제시합니다.
4. `API schema diff` 증거를 확보해 Frontend Developer에게 제시합니다.
5. PM에게 릴리스 일정과 승인 과정을 질문하고 `Release timeline` 증거를 확보합니다.
6. 필요하면 롤백을 지시한 뒤 최종 Incident Report를 제출합니다.

최종 원인은 QA 검증과 Frontend 반영이 완료되지 않은 상태에서 API 스키마 변경 후 배포가 진행된 것으로 정리할 수 있습니다.

## 프로젝트 구조

```text
Assets/OfficeMVP/Scenes/   게임 씬
Assets/OfficeMVP/Scripts/  이동, UI, 백엔드 통신, 액션 로직
Assets/OfficeMVP/Art/      캐릭터 및 오브젝트 에셋
Packages/                  Unity 패키지 설정
ProjectSettings/           Unity 프로젝트 설정
```

## 문제 해결

- `LOCAL OFFLINE`: 백엔드가 `127.0.0.1:8000`에서 실행 중인지 확인합니다.
- 연결 선택 화면에서 버튼이 비활성화됨: 해당 서버의 `/health` 상태가 `ONLINE`이 될 때까지 기다립니다.
- 대화 또는 액션이 실행되지 않음: 백엔드 세션이 준비되었는지, 플레이어가 대상 가까이에 있는지 확인합니다.
- 스크립트 오류가 발생함: Unity 버전이 `6000.4.0f1`인지 확인하고 패키지 임포트가 끝날 때까지 기다립니다.
