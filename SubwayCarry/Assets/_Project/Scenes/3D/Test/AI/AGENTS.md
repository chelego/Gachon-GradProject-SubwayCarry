# AI_V2 테스트 Scene 작업 지침

이 폴더 또는 하위 파일을 수정하기 전에 다음 문서를 순서대로 전부 읽는다.

1. 저장소 루트 `AGENTS.md`
2. `Assets/_Project/Scenes/Prototype/PassengerAI_V2/AGENTS.md`
3. `Assets/_Project/Scenes/Prototype/PassengerAI_V2/PASSENGER_AI_V2_GUIDE.md`
4. 이 폴더의 `README.md`

## Scene 생성 규칙

- 파일명은 `AI_V2_번호_검증대상_세부목적_Test.unity` 형식을 사용한다.
- 한 Scene은 한 단계의 문제만 재현한다.
- Scene 루트 이름에도 `TEST_번호_검증대상`을 넣는다.
- Game 화면 왼쪽 위에 Scene 이름, 검증 목적과 핵심 수치를 표시한다.
- 앞 단계가 통과하기 전에는 다음 단계의 빈 Scene을 미리 만들지 않는다.
- Scene을 다시 만드는 Builder가 있으면 수동 배치 변경이 사라지지 않도록 Builder를 함께 수정한다.

## 코드 및 성능 규칙

- V2 테스트에 필요한 파일은 이 폴더 아래에 모아 V1과 구분한다.
- 개별 승객의 전체 승객 순회와 매 프레임 Scene 검색을 금지한다.
- 근접 조회는 중앙 Spatial Hash를 사용하고, 인식 주기는 승객별로 분산한다.
- 그래픽과 움직임의 자연스러움은 사용자가 직접 Play Mode에서 판단한다.
- 컴파일, Console, Scene 연결과 자동 수치 검증을 시각 검증과 구분해 보고한다.
- 사용자 플레이 전에는 커밋하거나 Push하지 않는다.
