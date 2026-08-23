# Office Agent Incident Scenario Guide

## 1. 현재 상황

이 게임은 이미 배포 장애가 발생한 이후의 상황에서 시작합니다.

- PM이 릴리스 일정을 하루 앞당겼습니다.
- Backend Developer가 API 응답 스키마를 변경했습니다.
  - `response.data.items`
  - `response.payload.items`
- Frontend Developer는 API 변경사항을 늦게 전달받아 새 스키마를 반영하지 못했습니다.
- QA는 배포 20분 전 production-like 환경에서 API 응답 불일치를 발견했습니다.
- QA는 배포를 중단하고 계약 검증을 먼저 진행하라고 경고했습니다.
- 그러나 Backend Developer는 일정 압박 속에서 배포를 진행했습니다.
- 배포 후 Frontend 요청이 실패했고 서비스 장애가 발생했습니다.

플레이어의 목표는 각 담당자와 대화하고 증거를 확보해 장애의 직접 원인과 책임 구조를 파악하는 것입니다.

## 2. QA에게 먼저 질문하기

1. 게임을 시작하고 QA Desk로 이동합니다.
2. QA Engineer를 대화 대상으로 선택합니다.
3. 다음과 같이 일반적인 질문을 합니다.

   ```text
   배포 후 장애 원인이 뭐야?
   ```

4. 일반 질문에서는 정확한 에러명이나 경고 원문이 바로 공개되지 않습니다. QA는 배포 전 경고와 승인 과정을 먼저 확인해야 한다고 답합니다.
5. 구체적인 내용을 확인하려면 다음과 같이 증거를 요청합니다.

   ```text
   정확한 에러명과 QA 경고 원문을 보여줘.
   ```

6. `QA warning message` 증거가 확보됩니다.

   ```text
   [16:40] QA: Critical — API response mismatch found in production-like test.
   Recommend blocking deployment until the contract is verified.
   ```

7. 확보된 증거는 증거 목록에 표시되고, 확보 메시지는 빨간색으로 표시됩니다.

QA Desk의 `QA warning printout`을 조사해도 같은 증거를 확보할 수 있습니다.

## 3. QA에게 증거 제시하기

확보한 `QA warning message`를 QA에게 제시할 수 있습니다.

QA는 다음과 같은 취지로 반응합니다.

> 이 메시지는 제가 보낸 경고입니다. 이 경고가 어떻게 처리됐는지 확인해 주세요.

QA에게 증거를 제시하는 것은 선택 사항입니다. QA는 이미 자신이 작성한 경고를 알고 있으므로, 새로운 사실을 공개하기보다는 경고가 무시된 경위를 확인하도록 안내합니다.

## 4. Backend Developer에게 질문하기

1. 개발 구역으로 이동합니다.
2. Backend Developer를 대화 대상으로 선택합니다.
3. 다음과 같이 질문합니다.

   ```text
   API 변경과 배포 판단 과정을 설명해줘.
   ```

4. Backend Developer는 API 응답 스키마를 변경했고, 일정이 촉박해 배포를 진행했다고 설명합니다.
5. 다음과 같이 API 변경 증거를 요청합니다.

   ```text
   API 응답 스키마 변경 증거를 보여줘.
   ```

6. `API schema diff` 증거가 확보됩니다. 증거 내용은 기술적인 원문이므로 영문으로 표시될 수 있습니다.
7. `QA warning message`를 Backend Developer에게 제시합니다.

정상적인 반응은 다음과 같은 내용이어야 합니다.

> QA 경고가 있었던 것은 확인했습니다. 제가 API 응답 스키마를 변경한 상태에서 배포를 진행했고, 당시 판단 과정을 다시 검토하겠습니다.

Backend Developer는 QA 경고를 알고도 배포를 진행한 당사자이므로, 배포 사실을 부정해서는 안 됩니다.

## 5. Frontend Developer에게 질문하기

1. 개발 구역에서 Frontend Developer를 선택합니다.
2. 다음과 같이 질문합니다.

   ```text
   API 변경을 언제 전달받았어?
   ```

3. Frontend Developer는 API 변경을 늦게 전달받았으며, 마지막 로컬 검증은 통과했다고 설명합니다.
4. `API schema diff` 증거를 Frontend Developer에게 제시합니다.
5. Frontend Developer는 API 변경과 실제 반영 시점을 함께 검토하겠다고 반응합니다.

이를 통해 Backend의 API 변경과 Frontend의 늦은 반영 사이의 연관성을 확인할 수 있습니다.

## 6. PM / Planner에게 질문하기

1. PM Desk로 이동합니다.
2. PM / Planner에게 일정과 승인 과정을 질문합니다.

   ```text
   릴리스 일정이 왜 당겨졌어?
   ```

3. PM Desk의 `Release document`를 조사하거나 다음과 같이 일정 증거를 요청합니다.

   ```text
   배포 일정 증거를 보여줘.
   ```

4. `Release timeline` 증거가 확보됩니다.

   증거에는 다음 정보가 포함됩니다.

   - 릴리스 일정이 하루 앞당겨짐
   - 16:40 QA 경고
   - 17:00 Production 배포 시작

5. 이 증거를 PM에게 제시하면 일정 압박과 승인 과정을 함께 검토하게 됩니다.

## 7. 확보해야 할 핵심 증거

최종적으로 다음 세 가지 증거를 확보하는 것이 권장됩니다.

1. `QA warning message`
   - 배포 전 치명적인 API 응답 불일치
   - 배포 차단 권고
2. `API schema diff`
   - Backend API 스키마 변경
   - Frontend 반영 누락
3. `Release timeline`
   - 일정 단축
   - QA 경고 시각
   - 실제 배포 시각

증거 설명은 기술 원문이므로 영문으로 표시될 수 있습니다. NPC의 대화 반응은 한국어로 표시됩니다.

## 8. 사건 해결과 최종 보고서

조사를 마친 뒤 필요하면 다음 명령으로 롤백을 지시할 수 있습니다.

```text
배포 중단하고 롤백해
```

그 다음 최종 Incident Report를 작성합니다.

### Primary cause 예시

```text
Backend가 API 응답 스키마를 response.data.items에서 response.payload.items로 변경했고, Frontend 반영과 QA 검증이 완료되지 않은 상태에서 배포를 진행해 장애가 발생했습니다.
```

### Contributing factors 예시

```text
릴리스 일정이 하루 앞당겨짐
QA가 배포 차단을 권고했지만 승인 과정에서 확인되지 않음
API 변경사항이 Frontend에 늦게 전달됨
Production-like 환경의 계약 검증이 완료되지 않음
```

보고서를 제출하면 사건 상태가 `RESOLVED`로 변경되고, 장애 진단 점수·증거 확보율·팀 신뢰도·복구 효율이 결과로 표시됩니다.

소지품 던지기와 NPC 상호작용은 선택 기능이며, 조사와 최종 보고서 진행에는 필수가 아닙니다. 잘못된 물건을 던지면 신뢰도와 사건 결과에 영향을 줄 수 있습니다.
