# PassengerAI_V2

전체 역사와 열차 여정을 이해하고, 군중 속에서 사람처럼 행동하는 승객 AI를 새 구조로 검증하는 Prototype 폴더다.

현재 상태는 설계 기준과 작업 지침만 확정한 단계다. Runtime 코드와 테스트 Scene은 아직 만들지 않았다.

작업 전 읽을 파일:

1. 저장소 루트 `AGENTS.md`
2. 이 폴더의 `AGENTS.md`
3. `PASSENGER_AI_V2_GUIDE.md`

기존 `UtilityJourney_AI`는 V1 비교 기준으로 보존하며, V2 구현 중 직접 수정하거나 재생성하지 않는다.

초기 설계의 첫 구현 대상은 `PassengerAI_V2_SocialCorridor`였다. 현재 실행 가능한 01–06 테스트는 `Assets/_Project/Scenes/Ai_v2`의 README를 따른다. 이 폴더는 초기 설계 및 V1 비교 보존 지침이다.

성능 결과는 동일 Scene, 승객 수, Seed와 측정 시간의 Unity Profiler 전후 자료가 있을 때만 확정한다.
