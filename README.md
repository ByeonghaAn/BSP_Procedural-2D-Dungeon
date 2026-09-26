> 원본 오픈소스: [Sunny Valley Studio - Unity 2D Procedural Dungeon Tutorial](https://github.com/SunnyValleyStudio/Unity_2D_Procedural_Dungoen_Tutorial)  
> 이 프로젝트는 위 Unity 2D 절차적 던전 생성 프로젝트를 기반으로 제작했습니다. 원본의 방 생성 구조를 분석하고, 방 형태 다양화·역할 기반 분할·복도 연결 기능을 확장했습니다.

# BSP Procedural 2D Dungeon

BSP(Binary Space Partitioning) 기반의 2D 절차적 던전 생성 프로젝트입니다.

기본 BSP는 방을 생성할 수 있지만, 방의 게임 내 역할이나 디자이너가 원하는 배치 의도를 분할 과정에 반영하기 어렵습니다. 이 프로젝트에서는 방의 형태를 다양화하고, Start·Boss·Shop·Treasure 역할과 제약조건을 분할 기준에 도입했습니다.

## 주요 기능

### 템플릿 기반 방 생성

기본 직사각형 방의 일부를 빈 공간으로 만들어 형태를 변형합니다.

- `InnerEmptyRect`: 방 내부에 빈 공간 생성
- `SideEmptyRect`: 방의 한쪽 벽에 붙은 빈 공간 생성
- `EdgeEmptyRect`: 방 가장자리에 여러 빈 공간 생성

### 의미 기반 BSP

방의 역할에 따라 위치, 크기, 다른 역할과의 거리 조건을 설정합니다.

- Start: 맵 가장자리 선호
- Boss: 맵 중앙 선호, Start와 거리 확보
- Shop: Start와 거리 확보
- Treasure: Start와 설정된 거리 범위 유지

분할 단계마다 여러 후보를 생성하고, 역할별 제약조건을 얼마나 만족하는지 Fitness로 평가해 후보를 선택합니다.

### 복도 연결

Prim 알고리즘 기반 최소 신장 트리(MST)로 방의 기본 연결을 구성합니다. 이후 추가 복도를 생성해 우회 경로와 순환 구조를 만듭니다. 비정형 방에서는 실제 바닥 타일을 확인해 복도 연결점을 선택합니다.

### 생성 설정

- Unity Inspector에서 방 크기, 역할 설정, 추가 복도 수 등을 조절
- `RoomRole` 데이터 에셋으로 역할별 제약조건 관리
- Seed를 지정해 동일한 생성 결과 재현

## 실행 방법

1. 저장소를 내려받습니다.
2. Unity Hub에서 `Assets`, `Packages`, `ProjectSettings` 폴더가 들어 있는 프로젝트 루트를 엽니다.
3. 에셋 임포트가 완료되면 프로젝트의 씬을 열어 생성 기능을 확인합니다.

권장 Unity 버전은 `ProjectSettings/ProjectVersion.txt`를 확인해 주세요. `Library` 폴더는 저장소에 포함하지 않으며, 프로젝트를 열 때 Unity가 다시 생성합니다.

## 구현 범위와 한계

이 저장소는 제가 담당한 BSP 기반 던전 생성 프로젝트입니다. 팀 전체가 조사·구현한 다른 PCG 방식들을 하나의 완성된 툴로 통합한 결과물은 아닙니다.

분할 후보 선택에 사용한 탐욕 방식은 전역 최적해를 보장하지 않습니다. 또한 맵의 크기와 방 개수에 비해 추가 복도 수가 적으면 막다른 길이 늘어날 수 있어, 맵 규모에 따른 파라미터 조정이 필요합니다.

## 출처

- 원본 코드 기반: [Sunny Valley Studio - Unity 2D Procedural Dungeon Tutorial](https://github.com/SunnyValleyStudio/Unity_2D_Procedural_Dungoen_Tutorial)
