# PassengerAI V2 설계 및 최적화 기준

## 1. 목표

PassengerAI V2는 플랫폼이나 객차에 종속된 별도 AI가 아니라, 한 승객이 외부 출입구에서 목적지역 출구까지 일관된 기억과 성격을 유지하며 이동하는 자율 에이전트 시스템이다.

승객은 다음을 수행해야 한다.

- 주변 사람, 이동 방향, 속도, 밀도, 시설 상태와 열차 시간을 인식한다.
- 목적역까지 필요한 전체 여정과 현재 단계를 이해한다.
- 여러 가능한 시설과 행동 중 성격과 상황에 맞는 선택을 한다.
- 군중 흐름에 속도를 맞추고, 미리 비키거나 양보하며, 혼잡 시 개인 공간을 줄인다.
- 필요한 경우 가볍게 접촉할 수 있지만 장시간 교착하거나 진동하지 않는다.
- 기다리기, 줄서기, 앉기, 서기, 기대기, 걷기와 뛰기를 상황에 따라 전환한다.
- 실패한 행동을 감지하고 안전하게 다시 계획한다.

강화학습이나 머신러닝은 V2의 기본 전제가 아니다. 먼저 결정 과정을 설명하고 재현하며 디버깅할 수 있는 계층형 게임 AI를 만든다.

## 2. 전체 구조

```text
World Events / Smart Objects / Nearby Agents
                    |
                    v
             PassengerPerception
                    |
                    v
             PassengerBlackboard
                    |
        +-----------+-----------+
        |                       |
        v                       v
   JourneyPlanner         UtilityDecision
        |                       |
        +-----------+-----------+
                    v
              ActionExecutor
                    |
                    v
        GlobalPath + DesiredVelocity
                    |
                    v
            SocialNavigation
                    |
                    v
              PassengerMotor
```

### PassengerPerception

관찰 결과만 만든다. 직접 행동을 고르거나 이동하지 않는다.

- 주변 승객 위치, 속도, 이동 방향과 의도
- 개인 공간 안의 밀도와 평균 군중 흐름
- 문 열림, 닫힘까지 남은 시간과 하차 흐름
- 좌석, 대기 위치, 개찰구와 계단의 가용 상태
- 목적지까지의 예상 이동 시간과 막힘 상태

인식은 이벤트와 저주기 갱신을 병행한다. 문 상태처럼 즉시성이 필요한 변화는 이벤트로 깨우고, 군중 관찰은 분산된 주기로 갱신한다.

### PassengerBlackboard

한 승객의 지속 상태를 보관한다.

- 출발역, 목적역, 노선 방향과 선호 출구
- 현재 여정 단계와 선택한 행동
- 예약한 Smart Object와 실패 횟수
- 피로, 긴급도, 스트레스와 기다린 시간
- 최근 충돌, 양보, 막힘과 재계획 시각
- 성격과 보행 특성

Scene 전환이나 열차 탑승으로 공간이 바뀌어도 Blackboard는 유지한다.

### JourneyPlanner

HTN 방식으로 큰 목표를 실행 가능한 단계로 분해한다.

```text
ReachDestinationStation
|- EnterOriginStation
|  |- ChooseEntrance
|  |- PassEntryGate
|  `- ReachCorrectPlatform
|- RideTrain
|  |- ChooseWaitingBehavior
|  |- YieldToAlightingFlow
|  |- BoardTrain
|  |- ChooseTrainActivity
|  `- PrepareToAlight
`- ExitDestinationStation
   |- AlightTrain
   |- ReachExitConcourse
   |- PassExitGate
   `- LeavePreferredExit
```

계획은 매 프레임 다시 만들지 않는다. 단계 완료, 전제조건 상실, 시간 초과 또는 시설 고장처럼 의미 있는 사건에서만 다시 계획한다.

### UtilityDecision

현재 HTN 단계에서 허용된 선택지만 점수화한다.

예시:

```text
SeatScore
= fatigue * seatPreference
+ comfort
- distanceCost
- crowdCost
- exitPreparationCost

RunScore
= missTrainRisk
+ urgency
- crowdRisk
- collisionRisk
- mobilityPenalty
```

점수는 단순한 무작위값보다 정규화된 고려 요소와 Curve를 사용한다. 선택 후에는 Commitment, Hysteresis와 Cooldown을 적용해 작은 점수 변화로 행동이 계속 뒤집히지 않게 한다.

### ActionExecutor

선택한 행동을 여러 단계에 걸쳐 안전하게 완료한다.

- 전제조건 확인
- Smart Object 예약
- 접근 위치 이동
- 실제 상호작용과 통과
- 완료조건 확인
- 예약 해제
- 시간 초과와 실패 복구

카드 태그, 계단 진입, 열차 문 통과, 좌석 착석처럼 중간에 끊기면 문제가 생기는 행동은 Utility가 직접 실행하지 않는다.

### SocialNavigation

전역 경로가 만든 희망 방향과 속도를 군중 상황에 맞는 실제 속도로 바꾼다.

- Time To Collision 기반 사전 감속과 측면 회피
- 동일 방향 흐름의 평균 속도 추종
- 반대 방향 흐름의 통행 측 선택
- 밀도에 따른 개인 공간과 속도 조절
- 하차 우선과 문 주변 비움
- 최소 속도와 탈출 방향을 이용한 교착 해소
- 높은 밀도에서만 제한적인 접촉 허용

분리 힘을 단순 누적해 강제로 밀어내는 방식을 핵심으로 사용하지 않는다. 초기에는 TTC와 Flow Following으로 검증하고, 다중 충돌 제약이 부족하면 ORCA/RVO 계열 속도 선택을 도입한다.

### Smart Object

시설은 좌표가 아니라 사용 규칙을 제공한다.

```text
SmartObject
|- Affordance
|- Approach Anchors
|- Entry / Exit Anchors
|- Direction
|- Capacity
|- Reservation Queue
|- Preconditions
|- Completion Condition
`- Failure / Release Rules
```

개찰구, 계단, 열차 문, 좌석, 기대는 벽, 손잡이와 플랫폼 대기구역을 같은 계약으로 연결한다.

## 3. 성격과 상황 데이터

성격은 행동을 고정하는 타입 이름이 아니라 여러 연속값으로 표현한다.

- Courtesy
- Assertiveness
- PersonalSpace
- CrowdTolerance
- Patience
- SeatPreference
- DoorProximityPreference
- WalkingPace
- ReactionDelay
- RouteFamiliarity
- Mobility
- FatigueSensitivity
- RiskTolerance

현재 상황은 별도 값으로 유지한다.

- Urgency
- Fatigue
- CrowdStress
- TimeMargin
- CurrentDensity
- RecentFailureCount

동일한 성격이라도 열차 도착시간, 피로와 혼잡에 따라 다른 결정을 내리게 한다.

## 4. 성능 설계

### 업데이트 주기

| 작업 | 초기 기준 |
| --- | --- |
| 물리 이동과 최종 속도 적용 | FixedUpdate |
| 근거리 충돌 예측 | 10~20Hz, 승객별 분산 |
| 일반 주변 인식 | 5~10Hz, 승객별 분산 |
| Utility 재평가 | 2~4Hz 또는 중요 이벤트 |
| HTN 계획 | 단계 변경, 실패와 전제조건 상실 시 |
| 전역 경로 재탐색 | 목표 변경, 경로 무효화 또는 장기 정체 시 |
| 먼 승객 시뮬레이션 | 중요도에 따라 더 낮은 주기 |

이 값은 확정 성능 수치가 아니라 첫 프로파일링 기준이다. 실제 Profiler 결과로 조정한다.

### 중앙 CrowdManager

- Spatial Hash로 가까운 승객만 조회한다.
- 승객 등록과 해제를 중앙에서 관리한다.
- 인식, Utility, 경로 요청을 프레임별로 나눠 실행한다.
- 동일 시설 예약, 대기열과 이동 의도를 공유한다.
- 근처 밀도와 평균 흐름을 캐시해 여러 승객이 재사용한다.
- Path Queue에 프레임별 처리 예산을 둔다.
- Pooling을 사용해 반복 생성과 제거 비용을 줄인다.

### Hot Path 금지사항

- 전체 승객 배열 순회
- `FindObjectOfType`, `FindGameObjectsWithTag`와 Scene 전체 검색
- 매 프레임 LINQ
- 매 프레임 List, Dictionary, 배열과 문자열 생성
- 같은 목표에 대한 반복 A*
- 모든 승객의 같은 프레임 동시 판단
- 디버그 UI 문자열을 모든 승객이 계속 생성하는 구조

### 측정 항목

- Main Thread와 Physics CPU 시간
- `GC Alloc / Frame`
- 주변 승객 조회 수와 평균 반환 인원
- Utility 평가 횟수
- A* 요청, 성공, 실패와 캐시 적중률
- 3초 이상 교착 횟수
- 승객 접촉 횟수와 최대 겹침 시간
- 행동 전환 횟수
- 개찰구, 계단, 탑승과 하차 실패율

성능이 좋아졌다고 보고하려면 동일 Scene, 동일 승객 수, 동일 Seed와 동일 측정 시간의 전후 Profiler 결과가 있어야 한다.

## 5. 단계별 테스트 Scene

V2는 한 개의 거대한 통합 Scene부터 만들지 않는다.

1. `AI_V2_01_SocialCorridor_TwoWayAndFlow_Test`: 양방향 교차와 같은 방향 흐름
2. `AI_V2_02_StairMerge_NarrowEntrance_Test`: 좁은 계단 입구와 합류
3. `AI_V2_03_FareGate_QueueAndDirection_Test`: 예약, 줄서기와 방향 통과
4. `AI_V2_04_TrainDoor_AlightBeforeBoard_Test`: 하차 우선, 탑승과 문 닫힘
5. `AI_V2_05_TrainInterior_SeatStandLean_Test`: 좌석, 입석, 기대기와 하차 준비
6. `AI_V2_06_FullJourney_StationToStation_Test`: 외부 출구부터 목적지역 출구까지 전체 여정

각 Scene은 한 가지 문제를 재현하고, 자동 수치 검증과 사용자 Play Mode 확인을 분리한다.

## 6. 초기 합격 기준

- 두 승객이 마주쳤을 때 장시간 정지하거나 좌우 진동하지 않는다.
- 같은 방향 군중은 선두의 흐름을 따르되 완전히 겹치지 않는다.
- 계단, 개찰구와 문에서 3초 이상 교착하면 원인을 기록하고 탈출 또는 재계획한다.
- 하차 승객이 문 안전지점에 도달하기 전에 탑승 승객이 통로를 막지 않는다.
- 좌석과 한 사람용 시설은 중복 예약되지 않는다.
- 문이 닫히기 전에 탑승 또는 하차가 끝나지 않으면 열차 상태와 Action 실패 규칙이 일관되게 처리된다.
- 기준 인원에서 행동 검증을 통과한 뒤에만 승객 수를 늘린다.
- 자동 검증 통과와 자연스러운 시각 결과를 별도로 보고한다.

## 7. V1과의 관계

- `UtilityJourney_AI`는 기존 방식의 행동과 문제를 비교하는 Read-only 기준으로 남긴다.
- V2는 V1의 1,442줄 Brain을 직접 확장하지 않고 새 모듈로 작성한다.
- 재사용 전 기존 코드의 역할을 분리해 확인한다. 데이터, 계약과 순수 계산은 재사용할 수 있지만 Scene 좌표와 Prototype 전용 전환은 복사하지 않는다.
- V2 동작이 단계별로 검증된 뒤 필요한 모듈만 실제 통합 Scene으로 옮긴다.
- V1 삭제는 V2 전체 여정이 기능, 성능과 사용자 시각 검증을 모두 통과한 뒤 별도 작업으로 결정한다.

## 8. 예정 폴더

```text
Assets/_Project/Scenes/Ai_v2
|- AGENTS.md
|- README.md
|- AI_V2_01_SocialCorridor_TwoWayAndFlow_Test.unity
|- Scripts
|  |- Runtime
|  `- Editor
|- Profiles
`- Tests
```

폴더는 실제 구현이 시작될 때 필요한 단위만 추가한다. 비어 있는 폴더와 사용되지 않는 Scene을 미리 대량 생성하지 않는다.
