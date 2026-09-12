# 원본 유지 통합 검토 — 2026-09-13

## 수신 범위

| 담당 | 브랜치 / 반영 커밋 | 수신한 내용 |
| --- | --- | --- |
| 김준 (Trevorjun) | `role/player-gameplay` / `6406106` | 균형 QTE, 넘어짐·앉기 애니메이션, 운반 및 내구도별 피해 표현 |
| 정준호 (juno) | `feature-station` / `adc631e` | 기존 역·객차 씬, 스프라이트·문 프리팹과 좌석 아트 |
| 조재형 (Jodorong) | `role/delivery-economy` / `c11e2b7` | 배송, 정산·경제 및 강화 UI |
| 조재형 (Jodorong) | `stage-economy-test` / `edb8a41` | 역할 브랜치 이후의 `GameplayFlow_Prototype` 및 `ServiceShopPrototypeUI` 3개 파일 변경만 3-way 적용; 개인 복구·IDE 설정은 제외 |

팀 원본의 경로·GUID를 보존한다. `ArtMap_GameplaySlice`의 참조를 원본에 연결했고, 중복 `TeamReview` 코드는 더 이상 사용하지 않는다. 기존 AI 01–06, V1 비교용 여정 및 쿼터뷰 테스트는 보존한다. 폴더 수신과 통합 씬의 전체 기능 완성을 동일하게 취급하지 않는다.

## 이번 검사와 한계

- 중복 사본을 제외한 런타임 C# 131개와 Editor C# 14개의 독립 컴파일 통과. 기존 미사용 필드 경고 1개는 남아 있다.
- 통합 씬과 프리팹의 이전 TeamReview GUID·경로·타입 의존성 0개. 최종 프로젝트 자산 집합의 중복 GUID 및 누락 `.meta` 0개.
- 직렬화 참조 12,986개를 검사했다. 통합 폴더에는 검출 문제가 없지만 아래 원본·기존 설정의 미해결 항목은 남는다.
- Unity의 실제 재임포트, Play Mode, 시각 확인과 성능 측정은 하지 않았다. 사용자 확인이 필요하다. 기존 문서에 있는 2026-09-07 검증 수치는 이번 재실행 결과가 아니다.

## 원본 담당자와 확인할 항목

아래 항목은 원본 기능을 임의로 재구성하지 않고 남긴 검토 사항이다. 경로는 `SubwayCarry/Assets` 기준이다.

| 대상 / 담당 | 근거 | 다음 확인 |
| --- | --- | --- |
| `_Project/Scenes/Prototype_Core.unity` / 플레이어·경제 담당 | `packageDurabilityProviderSource`가 Player.prefab에서 제거된 컴포넌트의 fileID `4989290657511045195`를 경유해 참조한다. 최신 운반물은 별도 CakePackage 프리팹이다. | 원본 테스트 씬에서 새 운반물 생성 및 DeliveryService 공급자 연결을 담당자와 결정. 통합 씬은 별도로 CakePackage를 생성한다. |
| `_Project/Art/Sprites/Materials/bench.mat` / 맵·아트 담당 | `_MainTex`의 GUID `6678e02007c6e934b901b66554bde442`가 없다. 현재 검사에서 이 재질을 소비하는 자산 참조는 발견하지 못했다. | 누락 원본 텍스처 또는 미사용 제작용 재질 여부 확인. |
| `_Project/Art/Sprites/tile_palette/New Palette.prefab` / 맵·아트 담당 | 타일 GUID `1fa36a00d5bcd88419e9e06e0b1c6cd6`가 없다. | 타일 팔레트 제작용 원본 확인. |
| `Settings/Renderer2D.asset` / 공통 환경 담당 | 기존 `probeVolumeResources` 아래 디버그 자산 GUID 6개가 현재 패키지 캐시에 없다. 현재 Renderer2DData의 필드 구성과 오래된 직렬화 자료가 다르다. | 동일 Unity·패키지로 재임포트 후 의미 있는 경고인지 확인. 이번 정리를 이유로 렌더 설정을 바꾸지 않는다. |
| `_Project/Scenes/Prototype/GameplayFlow_Prototype.unity` 및 `Data/Delivery/DeliveryData_1-1.asset` / 해당 프로토타입 담당 | 총 10개의 `m_EditorClassIdentifier`가 이전 네임스페이스 이름이다. `m_Script` GUID는 현재 Prototype 스크립트를 가리킨다. | 실제 Unity 로딩·저장 시 확인. 문자열 차이만으로 런타임 오류라고 단정하지 않는다. |

## 팀에서 받는 방법

각 팀원은 미커밋 작업을 보존한 상태에서 원격을 fetch하고, 자신의 작업 브랜치에 `origin/develop`을 병합한다. 갈라진 이력을 강제 덮어쓰거나 폴더를 새로 복제하지 않는다. 이후 자기 담당 원본만 갱신해 올리고, 신찬욱이 `ArtMap_GameplaySlice`에서 연결·통합한다. 공통 파일은 편집 담당을 먼저 정한다. 자세한 기준은 [TEAM_WORKFLOW.md](TEAM_WORKFLOW.md)를 따른다.

팀원 PC를 직접 수정한 것은 아니다. 공통 Git 기준이 배포되어도 각자 기존 변경을 보존하며 받아야 환경이 맞춰진다.
