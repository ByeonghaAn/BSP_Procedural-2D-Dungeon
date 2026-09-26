> ## Original Project & Attribution
>
> This project is based on the following open-source project and tutorial by Sunny Valley Studio:
>
> - Original repository: [Unity 2D Procedural Dungeon Tutorial](https://github.com/SunnyValleyStudio/Unity_2D_Procedural_Dungoen_Tutorial)
> - Original tutorial video: [Unity Procedural Dungeon Generation 2D - Introduction](https://www.youtube.com/watch?v=-QOCX6SVFsk)
> - Original creator: Sunny Valley Studio
>
> The original project demonstrates 2D procedural dungeon generation in Unity using Random Walk, Binary Space Partitioning, rooms, corridors, and Unity Tilemaps. This repository uses the original project as a learning and development foundation, then extends the BSP-based dungeon generation system with template-based rooms, semantic room roles, constraint-based evaluation, Fitness-based partition selection, MST corridor connections, and additional corridors.
>
> Please refer to the original repository and video for the original implementation and tutorial.
>
> # BSP Procedural 2D Dungeon

BSP(Binary Space Partitioning) 기반의 2D 절차적 던전 생성 프로젝트입니다.

## Project Overview

이 프로젝트는 원본 Unity 2D Procedural Dungeon Tutorial을 기반으로 BSP 방식의 던전 생성을 분석하고, 레벨 디자이너의 의도를 반영할 수 있도록 생성 시스템을 확장한 프로젝트입니다.

기존 BSP는 공간을 분할해 방을 생성할 수 있지만, 각 방이 게임에서 어떤 역할을 해야 하는지 고려하지 못합니다. 또한 직사각형 중심의 단순한 방 형태와 트리 구조 중심의 연결로 인해 던전의 시각적 다양성과 탐험성이 제한될 수 있습니다.

이를 개선하기 위해 다음 기능을 구현했습니다.

- 템플릿 기반 비정형 방 생성
- 의미 기반 BSP
- 방 역할(Room Role) 기반 배치
- 하드 제약과 소프트 제약
- Fitness 기반 분할 후보 평가
- MST 기반 방 연결
- 추가 복도와 순환 경로 생성
- Seed 기반 맵 재현
- Unity Inspector 기반 파라미터 조정

## Original Source

원본 프로젝트는 Sunny Valley Studio의 Unity 2D Procedural Dungeon Tutorial입니다.

원본 프로젝트에서는 다음 내용을 다룹니다.

- Random Walk 기반 던전 생성
- Binary Space Partitioning 기반 방 생성
- 방과 복도 생성
- Rooms First 방식
- Corridors First 방식
- Unity Tilemap을 이용한 시각화

본 프로젝트는 원본의 구조와 구현 방식을 학습한 뒤, BSP 기반 생성 시스템을 중심으로 기능을 추가하고 개선했습니다.

## Main Improvements

### 1. Template-Based Room Generation

기본 BSP의 직사각형 방 형태를 개선하기 위해 방 내부 또는 가장자리에 빈 공간을 생성합니다.

- `InnerEmptyRect`
- `SideEmptyRect`
- `EdgeEmptyRect`

템플릿 적용 결과에 따라 `ㄷ`자, `+`자, `ㅁ`자와 같은 다양한 형태의 방을 생성할 수 있습니다.

### 2. Semantic BSP

기존 BSP는 공간의 크기와 분할 가능 여부만 판단하지만, Semantic BSP는 분할 기준에 방의 역할을 포함합니다.

지원하는 역할 예시는 다음과 같습니다.

- Start
- Boss
- Shop
- Treasure

각 역할은 위치, 크기, 다른 역할과의 거리 등에 대한 조건을 가질 수 있습니다.

예시:

- Start 방은 맵 가장자리를 선호합니다.
- Boss 방은 맵 중앙을 선호합니다.
- Boss 방은 Start 방과 일정 거리 이상 떨어져야 합니다.
- Shop 방은 Start 방과 일정 거리 이상 떨어질 수 있습니다.
- Treasure 방은 Start 방과 지정된 거리 범위 안에 배치될 수 있습니다.

이를 통해 디자이너는 방의 좌표를 직접 지정하지 않고도 역할과 제약조건을 통해 레벨 디자인 의도를 전달할 수 있습니다.

### 3. Fitness-Based Partition Selection

각 분할 단계에서 여러 후보를 생성하고, 후보별 역할 배치 결과를 시뮬레이션합니다.

후보는 다음 기준으로 평가됩니다.

- 방의 최소·최대 크기
- 역할별 방 개수
- 역할별 선호 위치
- 역할 간 거리
- 방의 선호 면적
- 하드 제약 위반 여부

가장 높은 Fitness를 가진 분할 후보를 선택해 BSP 트리를 확장합니다.

```text
Generate Candidates
        ↓
Assign Room Roles
        ↓
Check Hard Constraints
        ↓
Calculate Fitness
        ↓
Select the Best Candidate
```

### 4. MST-Based Corridor Connection

생성된 방을 그래프의 노드로 보고 방 사이의 거리를 간선 비용으로 사용합니다.

Prim 알고리즘 기반 MST를 통해 모든 방을 최소 비용으로 연결한 뒤, 일부 추가 복도를 생성해 우회 경로와 순환 구조를 만듭니다.

- MST: 모든 방의 기본 연결 보장
- 가까운 Floor Tile 탐색: 실제 이동 가능한 연결점 선택
- L자형 복도: Tilemap에 적합한 통로 생성
- Extra Corridor: 탐험 경로와 순환 구조 추가

### 5. Seed-Based Reproducibility

Seed 값을 지정하면 동일한 맵을 다시 생성할 수 있습니다.

Seed 시스템은 다음 작업에 활용할 수 있습니다.

- 버그 재현
- 알고리즘 수정 전후 비교
- 동일한 조건의 실험
- 결과 공유
- 레벨 테스트

## Project Structure

```text
Assets/
├─ Scenes/
├─ Scripts/
│  ├─ Dungeon/
│  ├─ Room/
│  ├─ Role/
│  ├─ Corridor/
│  └─ Visualization/
├─ Prefabs/
├─ ScriptableObjects/
├─ Sprites/
└─ Tilemaps/

Packages/
ProjectSettings/
```

프로젝트의 실제 폴더 구조에 맞게 위 목록은 수정해 주세요.

## How to Run

1. 저장소를 Clone합니다.
2. Unity Hub에서 프로젝트 루트를 엽니다.
3. `ProjectSettings/ProjectVersion.txt`에 기록된 Unity 버전을 확인합니다.
4. Unity가 패키지와 에셋을 임포트할 때까지 기다립니다.
5. 프로젝트의 메인 씬을 엽니다.
6. 던전 생성 컴포넌트의 Inspector에서 파라미터를 설정합니다.
7. Seed와 Room Role을 설정한 뒤 던전을 생성합니다.

## Experimental Comparison

기본 BSP와 Semantic BSP를 동일한 조건에서 비교했습니다.

- Map Size: 50×50, 100×100, 200×200
- Minimum Room Size: 10×10
- Start: 1
- Boss: 1
- Shop: 2
- Treasure: 2
- Minimum Distance: 30
- Maximum Distance: 60
- Extra Corridors: 3, 15, 30

평가 지표:

- Hard Constraint Violation Rate
- Overall Fitness
- Start-Boss Shortest Path
- Start-Shop Distance
- Start-Treasure Distance
- Dead-End Count
- Average Corridor Connections

실험 결과, Semantic BSP는 대부분의 조건에서 기본 BSP보다 높은 목적 함수 값을 기록했습니다. 다만 탐욕 알고리즘의 특성상 모든 경우에 전역 최적해를 보장하지는 않으며, 큰 맵에서는 추가 복도 수가 부족할 때 Dead-end가 증가하는 한계도 확인했습니다.

## Limitations

- 현재 분할 후보 선택은 탐욕 알고리즘 기반입니다.
- 전역 최적해를 항상 보장하지 않습니다.
- 맵 크기에 따라 Extra Corridor Count 조정이 필요합니다.
- 모든 PCG 알고리즘을 하나의 완성된 통합 툴로 결합한 단계는 아닙니다.
- 원본 프로젝트의 코드를 기반으로 학습하고 확장한 프로젝트입니다.

## Future Improvements

- Lookahead 기반 분할 후보 평가
- Rollback을 통한 이전 분할 복구
- 다른 PCG 방식과의 모듈형 통합

## Credits and References

- [Sunny Valley Studio - Unity 2D Procedural Dungeon Tutorial](https://github.com/SunnyValleyStudio/Unity_2D_Procedural_Dungoen_Tutorial)
- [Original Tutorial Video](https://www.youtube.com/watch?v=-QOCX6SVFsk)
- [Dungeon Asset Pack](https://pixel-poem.itch.io/dungeon-assetpuck)

Original project attribution:

> Made by Sunny Valley Studio
