# Passenger AI V2 테스트 Scene

이 폴더는 Passenger AI V2를 기능별로 분리해 직접 플레이하고 비교하기 위한 테스트 전용 폴더다.

## Scene 이름 규칙

```text
AI_V2_번호_검증대상_세부목적_Test.unity
```

Project 창과 Game 화면 왼쪽 위에서 현재 Scene의 번호와 검증 목적을 바로 확인할 수 있게 한다.

## Scene 목록

| 순서 | Scene 파일명 | 검증 내용 | 상태 |
| --- | --- | --- | --- |
| 01 | `AI_V2_01_SocialCorridor_TwoWayAndFlow_Test.unity` | 양방향 마주침, 사전 회피, 같은 방향 군중 속도 추종, 3초 교착 | 구현 |
| 02 | `AI_V2_02_StairMerge_NarrowEntrance_Test.unity` | 좁은 계단 입구 합류, 양방향 연속 통행과 계단 내부 무대기 | 구현 |
| 03 | `AI_V2_03_FareGate_QueueAndDirection_Test.unity` | 개찰구 선택, 줄서기, 카드 태그와 방향 고정 통과 | 구현 |
| 04 | `AI_V2_04_TrainDoor_AlightBeforeBoard_Test.unity` | 하차 우선, 문 옆 탑승 대기, 두 Lane 통과와 닫힘 대응 | 구현 |
| 05 | `AI_V2_05_TrainInterior_SeatStandLean_Test.unity` | `Prototype_AI` 두 객차 맵 재사용, Utility 좌석·입석·기대기 선택과 하차 준비 | 구현 |
| 06 | `AI_V2_06_FullJourney_StationToStation_Test.unity` | 역 입구 바깥부터 반대역 출구 바깥까지 한 승객의 전체 여정 | 구현 |

예정 Scene은 앞 단계의 교착, 충돌과 성능 기준을 통과한 뒤 하나씩 만든다. 빈 Scene은 미리 만들지 않는다.

## 현재 플레이 방법

1. `AI_V2_01_SocialCorridor_TwoWayAndFlow_Test.unity`를 연다.
2. Play를 누른다.
3. 청록색 승객과 주황색 승객이 서로 반대 방향으로 지나가는지 확인한다.
4. 바로 앞 승객뿐 아니라 선택 방향 앞쪽의 군중까지 보고 더 여유 있는 좌·우 또는 직진 감속을 선택하는지 확인한다.
5. 우측통행은 선호하되 우측이 붐비면 좌측으로도 피하고, 선택 방향이 매 순간 뒤집혀 진동하지 않는지 확인한다.
6. 빠른 승객이 앞 승객에게 붙었을 때 완전히 멈추지 않고 흐름 속도에 맞추는지 확인한다.
7. 화면 왼쪽 위 `Deadlocked`가 계속 0인지 확인한다.

## 02 플레이 방법

1. `AI_V2_02_StairMerge_NarrowEntrance_Test.unity`를 연다.
2. Play를 누른다.
3. 청록색 승객은 아래에서 위로, 주황색 승객은 위에서 아래로 이동하는지 확인한다.
4. 고정 우측 차선을 따르지 않고, 첫 진입자가 연 쪽과 같은 방향 승객의 움직임을 따라 양방향 물길이 자연스럽게 분리되는지 확인한다.
5. 이미 형성된 같은 방향 흐름에는 합류하고 반대 흐름이 차지한 길에는 억지로 몸을 밀어 넣지 않는지 확인한다.
6. 수용 인원을 넘는 혼잡에서만 입구 바깥에 대기하고 계단 내부에서는 3초 이상 멈추지 않는지 확인한다.
7. 화면 왼쪽 위 `Queue`, `Inside`, `Active L/U`, `Completed` 수치를 확인한다.

## 03 플레이 방법

1. `AI_V2_03_FareGate_QueueAndDirection_Test.unity`를 연다.
2. Play를 누른다.
3. 청록색 승객은 아래에서 위로 가는 두 개의 입장 게이트만, 주황색 승객은 위에서 아래로 가는 두 개의 퇴장 게이트만 고르는지 확인한다.
4. 거리와 현재 줄 길이에 따라 같은 방향의 두 게이트로 승객이 분산되는지 확인한다.
5. 승객이 카드 리더 앞에서 잠시 멈춘 뒤 주황색 차단봉이 초록색으로 열리고, 카드를 찍은 바로 그 통로로 지나가는지 확인한다.
6. 앞 승객의 몸 전체가 차단봉을 지난 직후 문이 닫히고, 이탈점까지 걷는 동안 다음 승객이 카드 리더로 이동하는지 확인한다.
7. 화면 왼쪽 위 `Waiting`, `Open`, `Card tags`, `Passed`, `Invalid pass` 수치를 확인한다.

## 04 플레이 방법

1. `AI_V2_04_TrainDoor_AlightBeforeBoard_Test.unity`를 연다.
2. Play를 누른다.
3. 청록색 하차 승객이 문 앞에서 과하게 감속하지 않고, 앞사람이 문턱을 넘는 즉시 다음 사람이 이어서 두 Lane으로 빠져나오는지 확인한다.
4. 주황색 탑승 승객이 반대편 빈 앞자리로 가로지르지 않고 처음 선택한 가까운 쪽 줄의 맨 뒤에 서는지 확인한다.
5. 하차 무리가 거의 빠지고 마지막 하차자가 진입 Lane을 빠져나올 때 다른 Lane부터 탑승이 시작되는지 확인한다.
6. 각 줄의 맨 앞 승객부터 문 통과권을 받되, 선두가 출발하면 뒤 승객들도 앞사람의 위치와 속도를 따라 줄 전체가 동시에 움직이며 주르륵 탑승하는지 확인한다.
7. `ClosingWarning` 동안 새 승객이 진입하지 않고, `Overlap`은 증가하되 `Invalid`, `Unsafe same-lane`, `Out-of-order`는 0인지 확인한다.

## 05 플레이 방법

1. `AI_V2_05_TrainInterior_SeatStandLean_Test.unity`를 연다.
2. Play를 누른다.
3. `Prototype_AI`의 두 객차 외형, 첫 객차의 좌석 48점과 손잡이 48점을 그대로 사용하는지 확인한다. 원래 V1 승객 Brain, Player와 Door Cycle은 이 복제 Scene에 포함하지 않는다.
4. 동일한 Base Passenger 60명이 거리, 자리 종류와 예약 상태를 Utility 점수로 비교해 좌석, 입석과 기대기로 나뉘는지 확인한다. 05의 승객 외형만 원본 객차 간격에 맞춰 기본 V2 크기보다 15% 작다.
5. 한 자리에 두 명이 겹치지 않고, 먼저 예약된 자리는 뒤 승객의 선택 후보에서 즉시 빠지는지 확인한다.
6. `PrepareToAlight`가 시작되면 앉거나 기대던 승객도 자리를 반납하고 실제 문 위치를 기준으로 만든 준비 위치로 이동하는지 확인한다.
7. 작은 점수 변화로 자리를 계속 바꾸지 않고 `Riding` 동안 선택한 행동을 유지하는지 확인한다.
8. 화면 왼쪽 위 `Invalid reservations`와 `Deadlocked`는 0, `Prepared`는 모든 승객 수까지 올라가는지 확인한다.

## 06 플레이 방법

1. `AI_V2_06_FullJourney_StationToStation_Test.unity`를 연다.
2. Play를 누른다.
3. 한 명의 Base Passenger가 출발역 입구 바깥에서 계단으로 들어와 입장 개찰구에 카드를 태그하는지 확인한다.
4. 개찰구 통과 후 플랫폼 계단에 들어가면 페이드 아웃되고, 이어 붙인 공간이 아니라 출발 플랫폼 Map으로 전환되는지 확인한다.
5. 열차를 기다린 뒤 문이 열리는 순서에 맞춰 탑승하고, 다시 페이드 전환된 `Prototype_AI` 객차에서 좌석·입석·기대기 Utility 행동 하나를 선택하는지 확인한다.
6. 하차 준비 후 목적지역 플랫폼 Map으로 전환되어 문을 통해 하차하고, 플랫폼 계단을 거쳐 목적지역 대합실 Map으로 이동하는지 확인한다.
7. 목적지역 출구 개찰구에 다시 카드를 태그하고 거리 출구 계단 바깥까지 나가면 `Completed 1`이 표시되는지 확인한다.
8. 왼쪽 위에서 `Entry tag`, `Board`, `Activity`, `Prepared`, `Alight`, `Exit tag`가 모두 `YES`, `Recoveries`와 `Deadlocked`는 0인지 확인한다.

06의 역 바깥은 전체 야외 이동 Map이 아니라 지하철 입구 계단 바깥의 시작·종료 지점만 표현한다. 한 Unity Scene 안에서도 다섯 Map Root 중 하나만 활성화해 화면을 옆으로 이어 붙이지 않는다.

## 현재 NPC 성격 기준

- 현재 모든 NPC는 `AI_V2_Profile_BasePassenger.asset` 하나를 공유한다.
- 성격값과 보행 속도의 NPC별 무작위 편차는 현재 `0`이다.
- 방향에 따른 몸 색상만 다르며 AI 판단 수치는 모두 같다.
- 이 공통 승객의 움직임을 기준 동작으로 먼저 확정한다.
- 이후 `SelfInterest(욕심)` 같은 성격값과 Blackboard의 `Urgency(현재 급함)`를 분리해 변형 NPC를 만든다.
- 다양한 프로필을 추가할 때도 Base Passenger Asset은 비교 기준으로 보존한다.

화면의 자연스러움은 사용자가 직접 판단한다. 자동 검증은 컴파일, Console, Scene 연결과 수치 검사만 근거로 사용한다.
