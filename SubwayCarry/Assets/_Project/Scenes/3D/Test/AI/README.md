# Passenger AI V2 테스트 Scene

이 폴더는 3D Passenger AI V2 테스트와 2D/3D 공용 AI 코드를 보관한다. 3D 01~05 씬은 이 폴더에, 기존 2D 01~06 씬은 `Assets/_Project/Scenes/2D/Test/AI`에 있다.

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

### 3D 테스트 01~05

- 이 폴더의 번호별 3D 씬을 열고 Play한다. 우클릭 드래그로 회전, 휠 버튼 드래그로 평면 이동, 휠로 확대·축소한다. WASD 이동, Q/E 높이, Shift 빠른 이동도 가능하다. 3D 테스트의 왼쪽 위 디버그 텍스트는 사용자 요청으로 표시하지 않는다.
- NPC는 Quaternius Universal Animation Library Standard의 CC0 마네킹이다. 원본 FBX·라이선스는 `Assets/ThirdParty/Quaternius/UniversalAnimationLibrary`에 있고, 01에는 대기·걷기·조깅을 연결한다. 기존 사람 모델은 사용하지 않는다.
- 기존 V2 판단·Spatial Hash와 동일한 Base Passenger를 유지한다. 3D 모드에서만 2D 물리를 끄고 XZ 평면의 3D 캡슐 Motor를 사용한다.
- 테스트장 모듈의 Blender 원본·생성 코드는 `Assets/Source~`, 엔진 모델·재질·프리팹은 같은 `Assets`의 용도별 하위 폴더에 둔다.
- 원래 2D 01~06은 비교 기준으로 보존한다. 3D 01은 기둥·안전 장벽·벤치를 우회하는 경로, 02는 실제 높이의 계단, 03은 방향별 개찰구, 04는 하차 우선 문, 05는 좌석·입석·기대기와 하차 준비를 분리해 시험한다. 사용자 요청으로 02~05를 함께 제작하지만 자동 검사와 사용자 플레이 승인은 별개다.
- 02~05는 기존 V2 Smart Object와 Action을 재사용하며 3D 시설 표현만 별도 어댑터로 연결한다. 착석은 원본 Sitting Enter/Idle/Exit 클립을 사용하고 2D식 스케일 압축을 하지 않는다.
- 3D 계단에서는 오르기 0.65m/s, 내려가기 0.78m/s의 수평 속도 상한을 적용하고 걷기 재생 속도도 맞춘다. 단 높이 변화는 수직 0.9m/s 이내에서 따라가며, 평지·기존 2D 속도에는 적용하지 않는다.
- 3D 생성 메뉴는 `SubwayCarry/Passenger AI V2/3D/Build 01 - Corridor + Animated Mannequin`이다. 이미 있는 3D 씬은 덮어쓰지 않는다.
- 추가 제작 메뉴는 `SubwayCarry/Passenger AI V2/3D/Apply obstacles 01 and build missing 02-05`다. 01은 장애물 구역만 추가하며, 이미 있는 02~05와 사용자가 저장하지 않은 편집 내용은 덮어쓰지 않는다.

아래는 원래 2D 테스트의 플레이 방법이다.

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
