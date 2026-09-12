# Passenger AI Prototype 정리 기록

작성일: 2026-08-31

## 유지

- `UtilityJourney_AI/UtilityJourney_AI.unity`
  - V1 한 명 집중 테스트 Scene이자 `BidirectionalTrainLoop_AI` 생성기의 맵 원본이다.
- `UtilityJourney_AI/BidirectionalTrainLoop_AI.unity`
  - V1 전체 여정과 양방향 승객의 비교 기준이다.
- `GameplayFlow_Prototype.unity`
  - 플레이어 게임 흐름 Prototype이므로 PassengerAI V2 정리 범위가 아니다.
- `QuarterView_Test`, `StationDirection_PlayerPrototype`과 기존 PassengerJourney Scene
  - 다른 기능 또는 이전 사용자 작업일 수 있어 현재 작업에서 임의 삭제하지 않는다.
- `Assets/TeamReview`
  - 2026-08-31에는 팀원 검토 자료로 보존했다. 2026-09-13 사용자 승인으로 원본 참조 전환·해시 검증 백업 후 중복 사본을 정리했다. 현재 원본 경로와 담당별 작업 규칙은 저장소 `docs/TEAM_WORKFLOW.md`를 따른다.
- `Assets/_Recovery/0.unity`
  - 출처가 확실하지 않은 복구 Scene이므로 보존한다.

## 삭제 완료

- `Assets/_Project/Scenes/Prototype/UtilityJourney_AI/Recovery`
  - 현재 통합 Scene을 고치기 전에 에이전트가 만든 복구 Scene 2개다.
  - 현재 Scene이나 Build Settings에서 참조하지 않는다.
  - 사용자 영구 삭제 승인 후 2026-08-31에 Asset과 `.meta`를 함께 삭제했다.
- `Assets/Screenshots`
  - 과거 자동·시각 검증 이미지 10개다.
  - 프로젝트 Asset에서 참조하지 않으며 기능 Asset이 아니다.
  - 사용자 영구 삭제 승인 후 2026-08-31에 이미지와 `.meta`를 함께 삭제했다.

## 정리 원칙

- Scene 이름만으로 삭제하지 않는다.
- Build Settings, GUID 참조와 생성기 입력 여부를 확인한다.
- 다른 팀원 또는 사용자가 만든 가능성이 있는 Asset은 보존한다.
- V1 삭제는 V2 전체 여정의 기능, 성능과 사용자 시각 검증이 끝난 뒤 별도로 결정한다.
