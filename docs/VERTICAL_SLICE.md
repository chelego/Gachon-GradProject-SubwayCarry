# 버티컬 슬라이스 — 아트 맵 / 플레이어 / 승객 AI

## 실행

Unity 6000.3.7f1에서 `SubwayCarry` 프로젝트를 연 뒤 아래 Scene을 연다.

`Assets/_Project/Scenes/Prototype/ArtMap_GameplaySlice/ArtMap_01_Gachon_Train_Wangsimni_Playable.unity`

새로 생성하는 메뉴를 누를 필요 없이 저장된 Scene을 실행한다. 맵 배치 수정 후 Builder로 덮어쓰지 않는다.

- 가천대역 ↔ 객차 ↔ 왕십리역: 문 표시 근처에서 E, 페이드 맵 이동.
- WASD 이동 / 마우스 바라보기 / 휠 확대·축소.
- 플레이어와 상자는 청록색 윤곽으로 구분한다.
- 1~5 자세 선택, 6~9 열차 균형 입력 시험.
- NPC는 기존 V2 이동·회피를 사용하며, 좌석/기대기/입석 대기를 유지하고 통과 목적 승객은 맵 밖으로 나간다.
- F6/F7/F8은 열차 접근/문 열림/출발이라는 AI 상황 신호만 보낸다. 열차 운행/문 아트/실제 역간 승하차를 자동 실행하지 않는다.

## 포함 범위

- 정준호 `feature-station` / `adc631e`: 역·객차 맵과 아트 의존 자료.
- 김준 `role/player-gameplay` / `040c0f9`: 플레이어/운반물/자세/파손/균형 및 필요한 공통 계약·스프라이트·프리팹만 포함.
- 버티컬 슬라이스 통합 씬과 코드: 아트 깊이/가림/크기 보정, 이동 가능 영역, 맵 전환, 플레이어-NPC 접촉 정보 연결.
- 공통 AI 추가: 장소 정보 계약, 상황별 Utility 행동, 상태 유지/이벤트 반응, 경로 코너 진행 수정.

`Assets/TeamReview/01_JeongJunho_Station`과 `02_KimJun_Player`는 이 씬의 실제 의존 파일이다. 임시 다운로드 폴더로 생각해서 삭제하면 안 된다. 서로 다른 구현이 충돌하지 않게 GUID와 네임스페이스를 분리한 복사본이며, 각 `IMPORT_MANIFEST.json`에 출처를 남겼다. 경제 검토본, 별도 플레이어 검토 씬, 캐시/복구 파일은 포함하지 않았다.

기존 `Ai_v2` 01~06 씬과 BasePassenger 프로필은 이번 업로드에서 변경하지 않는다. 공용 Agent 변경은 이 슬라이스에서만 켜는 선택적 이동 공간/플레이어 인식 연결부다.

## 검증과 미완성 범위

- 작업 프로젝트에서 비시각 코드 검사: 행동/경로 32개, 실제 맵 90개 경로와 벽 205지점 검사 통과.
- 최신 원격 develop에 통합한 파일 구성으로 Runtime/Editor C#을 Unity 6000.3.7f1 컴파일러와 설치된 패키지 참조를 이용해 별도 컴파일. Scene/GUID/.meta 의존성도 파일 기준 확인.
- 설치된 패키지 참조를 재사용한 컴파일이며, 별도 클린 Unity 임포트나 플레이 테스트를 대신하지 않는다.
- 원격 화면 조작/Play Mode 실행/Profiler 측정은 하지 않았다. 실제 자연스러움과 최종 시각 확인은 사용자/팀원이 한다.
- 전체 배송 경제/정산, 자동 열차 시간표, 문 개폐·역간 NPC 여정의 완성판은 아니다. 맵·아트·플레이어·승객을 함께 보는 통합 프로토타입이다.

[세부 조작과 설정](../SubwayCarry/Assets/_Project/Scenes/Prototype/ArtMap_GameplaySlice/README.md) / [AI 구조·조정값·참고 자료](../SubwayCarry/Assets/_Project/Scenes/Prototype/ArtMap_GameplaySlice/NPC_CONTEXT_BEHAVIOR_NOTES.md)
