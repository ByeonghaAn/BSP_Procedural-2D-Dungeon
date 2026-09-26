using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;
//Math 클래스 (수정)
using static System.Math;
using Random = UnityEngine.Random;

public class RoomFirstDungeonGenerator : RandomWalkDungeonGenerator
{
    [SerializeField]
    private int minRoomWidth = 10, minRoomHeight = 10;

    [SerializeField]
    private int dungeonWidth = 100, dungeonHeight = 100;
    
    // prevent the floor of each rooms connected to each other
    [SerializeField]
    [Range(0, 3)]
    private int offset = 1;

    // checking if we want to use random walk to create a room
    // or generate a square room using bounding box
    [SerializeField]
    private bool randomWalkRooms = false, templetRooms = false; // 수정

    [SerializeField] private RoleManager roleManager; // 수정

    [Header("Seed 설정")] // 수정
    [SerializeField] private int seed = 12345;

    [Header("templetRooms 추가 복도 설정")] // 수정
    [SerializeField] private int extraCorridorCount = 5; // 추가할 복도 개수
    private float maxExtraCorridorDistance = 50f; // 추가 복도 최대 거리
    private float extraCorridorProbability = 0.5f; // 추가 복도 생성 확률

    protected override void RunProceduralGeneration() 
    {
        CreateRooms();
    }

    private void CreateRooms()
    {
        Random.InitState(seed);

        var room_list = ProceduralGenerationAlgorithms.BinarySpacePartitioning(new BoundsInt((Vector3Int)startPosition, new Vector3Int(dungeonWidth, dungeonHeight, 0)),
                                                                                minRoomWidth,
                                                                                minRoomHeight);

        HashSet<Vector2Int> floor = new HashSet<Vector2Int>();

        // if the random walk turned on then we will use random walk to create a room
        if (randomWalkRooms)
        {
            floor = CreateRoomsRandomly(room_list);
        }
        // templetRooms가 true인 경우 템플릿 및 의미기반BSP 사용 (수정)
        else if (templetRooms)
        {
            List<RoomRole> roles = roleManager.GetAllRoles();

            // SemanticBspGenerator 인스턴스 생성
            var generator = new SemanticBspGenerator(
                new BoundsInt(0, 0, 0, dungeonWidth, dungeonHeight, 1),
                roles
            );

            // 의미 기반 BSP 실행
            room_list = generator.SemanticBinarySpacePartitioning(
                new BoundsInt(0, 0, 0, dungeonWidth, dungeonHeight, 1),
                minRoomWidth,
                minRoomHeight
            );

            floor = CreateTempletRooms(room_list);
        }
        else
        {
            // 일반 BSP 실행
            // 전체 맵 영역 정의
            BoundsInt mapBounds = new BoundsInt((Vector3Int)startPosition, new Vector3Int(dungeonWidth, dungeonHeight, 0));

            // RoleManager에서 역할 리스트 가져오기
            List<RoomRole> roles = roleManager.GetAllRoles();

            // 역할 배치 수행
            var assignedByRole = RoleAssigner.AssignRoles(room_list, mapBounds, roles);

            // 역할별로 배치된 방 정보와 Fitness 출력
            float totalMapFitness = 0f;

            foreach (var kvp in assignedByRole)
            {
                string roleName = kvp.Key;
                var roleInfo = kvp.Value;

                foreach (var room in roleInfo.Rooms)
                {
                    Debug.Log(
                        $"역할: {roleName}, " +
                        $"위치: {room.Bounds.position}, 크기: {room.Bounds.size}, Fitness: {room.Fitness}"
                    );
                    totalMapFitness += room.Fitness;
                }
            }
            Debug.Log($"[전체 맵 Fitness]: {totalMapFitness}");

            floor = CreateSimpleRooms(room_list);
        }

        List<Vector2Int> room_centers = new List<Vector2Int>();

        // add the each center of the room into the list
        foreach(var room in room_list)
        {
            // .center is Vector3Int, so we need to convert it to Vector2Int
            room_centers.Add(((Vector2Int)Vector3Int.RoundToInt(room.center)));
        }

        HashSet<Vector2Int> corridors = ConnectRooms(room_centers);

        //templetRooms가 true인 경우 ConnectRoomsWithMST 사용 (수정)
        if (templetRooms)
        {
            HashSet<(int, int)> mstEdges; // MST 엣지 저장
            corridors = ConnectRoomsWithMST(room_list, floor, out mstEdges);

            corridors = AddExtraLoops(room_list, floor, corridors, mstEdges); // 추가 복도 생성
        }

        // spawn the floor tiles for those corridors
        floor.UnionWith(corridors);

        tilemapVisualizer.PaintFloorTiles(floor);
        WallGenerator.CreateWalls(floor, tilemapVisualizer);                                                                
    }

    private HashSet<Vector2Int> CreateRoomsRandomly(List<BoundsInt> room_list)
    {
        HashSet<Vector2Int> floor = new HashSet<Vector2Int>();
        for(int i = 0; i < room_list.Count; i++)
        {
            var room_bounds = room_list[i];
            var room_center = new Vector2Int(Mathf.RoundToInt(room_bounds.center.x), Mathf.RoundToInt(room_bounds.center.y));
            
            // run the random walk with start position is the center of the room 
            var room_floor = RunRandomWalk(randomWalkParameters, room_center);
            
            // count the offset
            foreach(var position in room_floor)
            {
                if(position.x >= (room_bounds.xMin + offset) && position.x <= (room_bounds.xMax - offset) &&
                   position.y >= (room_bounds.yMin + offset) && position.y <= (room_bounds.yMax - offset))
                {
                    floor.Add(position);
                }
            }
        }

        return floor;
    }

    private HashSet<Vector2Int> ConnectRooms(List<Vector2Int> room_centers)
    {
        HashSet<Vector2Int> corridors = new HashSet<Vector2Int>();
        var current_room_center = room_centers[Random.Range(0, room_centers.Count)];
        // remove the selected room center from the list
        room_centers.Remove(current_room_center);
        
        // if the room center is greater than zero, then we need to connect it to the previous room
        while(room_centers.Count > 0)
        {
            Vector2Int closest_room_center = FindClosestPointTo(current_room_center, room_centers);
            room_centers.Remove(closest_room_center);
            HashSet<Vector2Int> newCorridor = CreateCorridor(current_room_center, closest_room_center);
            current_room_center = closest_room_center;
            corridors.UnionWith(newCorridor);
        }

        return corridors;
    }

    private HashSet<Vector2Int> CreateCorridor(Vector2Int current_room_center, Vector2Int destination)
    {
        HashSet<Vector2Int> corridor = new HashSet<Vector2Int>();
        var position = current_room_center;
        corridor.Add(position);

        // iterate until y position == y destination
        // to move the position up and down
        while(position.y != destination.y)
        {   
            if(destination.y > position.y) 
            {
                position += Vector2Int.up;
            }
            else if(destination.y < position.y)
            {
                position += Vector2Int.down;
            }

            corridor.Add(position);
        }

        // same iterate but using x to right and left
        while(position.x != destination.x) 
        {
            if(destination.x > position.x)
            {
                position += Vector2Int.right;
            } 
            else if(destination.x < position.x) 
            {
                position += Vector2Int.left;
            }

            corridor.Add(position);
        }

        return corridor;
    }

    private Vector2Int FindClosestPointTo(Vector2Int current_room_center, List<Vector2Int> room_centers)
    {
        Vector2Int closest = Vector2Int.zero;
        float distance = float.MaxValue;

        foreach(var position in room_centers)
        {
            float current_distance = Vector2.Distance(position, current_room_center);
            // check if the current distance is smaller than the previous distance
            // add to the closest point
            if(current_distance < distance) 
            {
                distance = current_distance;
                closest = position;
            }
        }

        return closest;
    }

    private HashSet<Vector2Int> CreateSimpleRooms(List<BoundsInt> room_list)
    {
        HashSet<Vector2Int> floor = new HashSet<Vector2Int>();
        foreach(var room in room_list) 
        {
            for(int col = offset; col < room.size.x - offset; col++)
            {
                for(int row = offset; row < room.size.y - offset; row++)
                {
                    Vector2Int position = (Vector2Int)room.min + new Vector2Int(col, row);
                    floor.Add(position);
                }
            }
        }

        return floor;
    }

    //CreateTempletRooms 함수 (수정)
    private HashSet<Vector2Int> CreateTempletRooms(List<BoundsInt> room_list)
    {
        HashSet<Vector2Int> floor = new HashSet<Vector2Int>();
        foreach (var room in room_list)
        {
            // 빈 영역 emptyRect 초기화
            List<BoundsInt> emptyRects = new List<BoundsInt>();

            // 랜덤으로 템플릿을 선택
            int chance = Random.Range(0, 100);
            if (chance < 30)
            {
                var r = InnerEmptyRect(room);
                emptyRects.Add(r);
            }
            else if (chance < 60)
            {
                var r = SideEmptyRect(room);
                emptyRects.Add(r);
            }
            else if (chance < 90)
            {
                var r = EdgeEmptyRect(room);
                emptyRects.AddRange(r);
            }

            for (int col = offset; col < room.size.x - offset; col++)
            {
                for (int row = offset; row < room.size.y - offset; row++)
                {
                    Vector2Int position = (Vector2Int)room.min + new Vector2Int(col, row);

                    // 빈 영역 emptyRect에 포함되는지 확인
                    bool inAnyEmpty = false;
                    for (int i = 0; i < emptyRects.Count; i++)
                    {
                        BoundsInt er = emptyRects[i];
                        if (position.x >= er.xMin && position.x < er.xMax &&
                            position.y >= er.yMin && position.y < er.yMax)
                        {
                            inAnyEmpty = true;
                            break;
                        }
                    }

                    // 빈 영역 emptyRect에 포함되면 추가하지 않음
                    if (inAnyEmpty)
                        continue;
                    floor.Add(position);
                }
            }
        }

        return floor;
    }

    //InnerEmptyRect 함수 (수정)
    private BoundsInt InnerEmptyRect(BoundsInt room)
    {
        // 방 안에 작은 사각형을 생성 -> 작은 사각형만큼 빈공간
        int emptyWidth = Random.Range(3, Max(4, room.size.x - offset * 4));
        int emptyHeight = Random.Range(3, Max(4, room.size.y - offset * 4));
        int emptyX = room.min.x + Random.Range(offset + 1, room.size.x - emptyWidth - offset - 1);
        int emptyY = room.min.y + Random.Range(offset + 1, room.size.y - emptyHeight - offset - 1);
        BoundsInt emptyRect = new BoundsInt(
            new Vector3Int(emptyX, emptyY, 0),
            new Vector3Int(emptyWidth, emptyHeight, 1)
        );

        return emptyRect;
    }

    //SideEmptyRect 함수 (수정)
    private BoundsInt SideEmptyRect(BoundsInt room)
    {
        // 방의 벽에 붙은 작은 사각형을 생성 -> 작은 사각형만큼 빈공간
        int emptyWidth = Random.Range(3, Max(4, room.size.x - offset * 4));
        int emptyHeight = Random.Range(3, Max(4, room.size.y - offset * 4));

        // 방의 어느 벽에 붙을지 랜덤으로 선택
        int chance = Random.Range(0,4);
        int emptyX, emptyY = 0;
        if (chance == 0) // 방의 위쪽 벽
        {
            emptyX = room.min.x + Random.Range(0, room.size.x - emptyWidth - offset);
            emptyY = room.max.y - emptyHeight;
        }
        else if (chance == 1) // 방의 아래쪽 벽
        {
            emptyX = room.min.x + Random.Range(0, room.size.x - emptyWidth - offset);
            emptyY = room.min.y;
        }
        else if (chance == 2) // 방의 왼쪽 벽
        {
            emptyX = room.min.x;
            emptyY = room.min.y + Random.Range(0, room.size.x - emptyHeight - offset);
        }
        else // 방의 오른쪽 벽
        {
            emptyX = room.max.x;
            emptyY = room.min.y + Random.Range(0, room.size.x - emptyHeight - offset);
        }

        BoundsInt emptyRect = new BoundsInt(
            new Vector3Int(emptyX, emptyY, 0),
            new Vector3Int(emptyWidth, emptyHeight, 1)
        );

        return emptyRect;
    }

    //EdgeEmptyRect 함수 (수정)
    private List<BoundsInt> EdgeEmptyRect(BoundsInt room)
    {
        List<BoundsInt> emptyRects = new List<BoundsInt>();

        // 방의 가장자리에 사각형을 생성 -> 작은 사각형만큼 빈공간
        int emptyWidth = Random.Range(3, Max(4, room.size.x / 2 - offset));
        int emptyHeight = Random.Range(3, Max(4, room.size.y / 2 - offset));

        int emptyLeftX = room.min.x;
        int emptyRightX = room.max.x - emptyWidth;
        int emptyUpY = room.max.y - emptyHeight;
        int emptyDownY = room.min.y;

        BoundsInt LDRect = new BoundsInt(
                new Vector3Int(emptyLeftX, emptyDownY, 0),
                new Vector3Int(emptyWidth, emptyHeight, 1)
            );
        BoundsInt LURect = new BoundsInt(
            new Vector3Int(emptyLeftX, emptyUpY, 0),
            new Vector3Int(emptyWidth, emptyHeight, 1)
        );
        BoundsInt RURect = new BoundsInt(
                new Vector3Int(emptyRightX, emptyUpY, 0),
                new Vector3Int(emptyWidth, emptyHeight, 1)
            );
        BoundsInt RDRect = new BoundsInt(
            new Vector3Int(emptyRightX, emptyDownY, 0),
            new Vector3Int(emptyWidth, emptyHeight, 1)
        );

        int chance = Random.Range(0, 9);
        if (chance == 0) //좌하단, 우하단
        {
            emptyRects.Add(LDRect);
            emptyRects.Add(RDRect);
        } 
        else if(chance == 1) //좌하단, 좌상단
        {
            emptyRects.Add(LDRect);
            emptyRects.Add(LURect);
        }
        else if (chance == 2) //좌상단, 우상단
        {
            emptyRects.Add(LURect);
            emptyRects.Add(RURect);
        }
        else if (chance == 3) //우상단, 우하단
        {
            emptyRects.Add(RURect);
            emptyRects.Add(RDRect);
        }
        else if (chance == 4) //좌하단, 우하단, 좌상단
        {
            emptyRects.Add(LDRect);
            emptyRects.Add(RDRect);
            emptyRects.Add(LURect);
        }
        else if (chance == 5) //좌하단, 좌상단, 우상단
        {
            emptyRects.Add(LDRect);
            emptyRects.Add(LURect);
            emptyRects.Add(RURect);
        }
        else if (chance == 6) //좌상단, 우상단, 우하단
        {
            emptyRects.Add(LURect);
            emptyRects.Add(RURect);
            emptyRects.Add(RDRect);
        }
        else if (chance == 7) //우상단, 우하단, 좌하단
        {
            emptyRects.Add(RURect);
            emptyRects.Add(RDRect);
            emptyRects.Add(LDRect);
        }
        else if (chance == 8) //좌하단 & 좌상단 & 우상단 & 우하단
        {
            emptyRects.Add(LDRect);
            emptyRects.Add(LURect);
            emptyRects.Add(RURect);
            emptyRects.Add(RDRect);
        }

        return emptyRects;
    }

    //ConnectRoomsWithMST 함수 (수정)
    // ConnectRoomsWithMST 메서드 수정 (MST 엣지를 반환하도록)
    private HashSet<Vector2Int> ConnectRoomsWithMST(List<BoundsInt> room_list, HashSet<Vector2Int> floor, out HashSet<(int, int)> mstEdges)
    {
        mstEdges = new HashSet<(int, int)>();

        if (room_list.Count == 0)
            return new HashSet<Vector2Int>();

        HashSet<Vector2Int> corridors = new HashSet<Vector2Int>();
        HashSet<int> connected = new HashSet<int>();
        List<(int from, int to, float distance)> edges = new List<(int, int, float)>();

        int startRoom = Random.Range(0, room_list.Count);
        connected.Add(startRoom);

        while (connected.Count < room_list.Count)
        {
            edges.Clear();

            foreach (int connectedIdx in connected)
            {
                for (int i = 0; i < room_list.Count; i++)
                {
                    if (!connected.Contains(i))
                    {
                        float dist = GetRoomDistance(room_list[connectedIdx], room_list[i], floor);
                        edges.Add((connectedIdx, i, dist));
                    }
                }
            }

            if (edges.Count == 0)
                break;

            edges.Sort((a, b) => a.distance.CompareTo(b.distance));
            var shortestEdge = edges[0];

            // MST 엣지 저장 (작은 인덱스를 먼저)
            int minIdx = Mathf.Min(shortestEdge.from, shortestEdge.to);
            int maxIdx = Mathf.Max(shortestEdge.from, shortestEdge.to);
            mstEdges.Add((minIdx, maxIdx));

            HashSet<Vector2Int> newCorridor = CreateCorridorBetweenRooms(
                room_list[shortestEdge.from],
                room_list[shortestEdge.to],
                floor
            );
            corridors.UnionWith(newCorridor);

            connected.Add(shortestEdge.to);
        }

        return corridors;
    }

    // 두 방 사이의 최단 거리 (floor 파라미터 추가)
    private float GetRoomDistance(BoundsInt roomA, BoundsInt roomB, HashSet<Vector2Int> floor)
    {
        Vector2Int closestA = GetClosestTileToRoom(roomA, roomB, floor);
        Vector2Int closestB = GetClosestTileToRoom(roomB, roomA, floor);
        return Vector2Int.Distance(closestA, closestB);
    }

    // roomA에서 roomB로 가장 가까운 실제 타일 찾기 (수정)
    private Vector2Int GetClosestTileToRoom(BoundsInt roomA, BoundsInt roomB, HashSet<Vector2Int> floor)
    {
        Vector2Int closest = Vector2Int.zero;
        float minDistance = float.MaxValue;
        Vector2Int roomBCenter = (Vector2Int)Vector3Int.RoundToInt(roomB.center);
        bool foundAny = false;

        // roomA의 모든 타일 검사 (가장자리 우선)
        for (int x = roomA.xMin + offset; x < roomA.xMax - offset; x++)
        {
            for (int y = roomA.yMin + offset; y < roomA.yMax - offset; y++)
            {
                Vector2Int tilePos = new Vector2Int(x, y);

                // ===== 핵심: 실제 floor에 타일이 있는지 확인 =====
                if (!floor.Contains(tilePos))
                    continue;

                // 가장자리 타일 우선 (더 빠른 연결을 위해)
                bool isEdge = (x == roomA.xMin + offset || x == roomA.xMax - offset - 1 ||
                              y == roomA.yMin + offset || y == roomA.yMax - offset - 1);

                float distance = Vector2Int.Distance(tilePos, roomBCenter);

                // 가장자리 타일이면 우선순위 부여
                if (isEdge)
                    distance -= 0.1f; // 약간의 가중치

                if (distance < minDistance)
                {
                    minDistance = distance;
                    closest = tilePos;
                    foundAny = true;
                }
            }
        }

        // 만약 타일을 못 찾았다면 방 중심점 반환 (안전장치)
        if (!foundAny)
        {
            closest = (Vector2Int)Vector3Int.RoundToInt(roomA.center);
        }

        return closest;
    }

    // 두 방을 가장 가까운 실제 타일끼리 연결 (floor 파라미터 추가)
    private HashSet<Vector2Int> CreateCorridorBetweenRooms(BoundsInt roomA, BoundsInt roomB, HashSet<Vector2Int> floor)
    {
        Vector2Int startTile = GetClosestTileToRoom(roomA, roomB, floor);
        Vector2Int endTile = GetClosestTileToRoom(roomB, roomA, floor);

        return CreateCorridor(startTile, endTile);
    }

    // 모든 방 쌍의 엣지 계산
    private List<(int, int, float)> BuildAllRoomEdges(List<BoundsInt> room_list, HashSet<Vector2Int> floor)
    {
        var edges = new List<(int, int, float)>();

        for (int i = 0; i < room_list.Count; i++)
        {
            for (int j = i + 1; j < room_list.Count; j++)
            {
                float dist = GetRoomDistance(room_list[i], room_list[j], floor);
                edges.Add((i, j, dist));
            }
        }

        edges.Sort((e1, e2) => e1.Item3.CompareTo(e2.Item3)); // Item3 = 거리
        return edges;
    }

    // 복도가 맵 밖으로 나가는지 검사
    private bool IsCorridorOutOfBounds(HashSet<Vector2Int> corridor)
    {
        BoundsInt mapBounds = new BoundsInt(
            (Vector3Int)startPosition,
            new Vector3Int(dungeonWidth, dungeonHeight, 0)
        );

        foreach (var tile in corridor)
        {
            if (tile.x < mapBounds.xMin || tile.x >= mapBounds.xMax ||
                tile.y < mapBounds.yMin || tile.y >= mapBounds.yMax)
            {
                return true; // 맵 밖으로 나감
            }
        }

        return false; // 맵 안에 있음
    }

    // 추가 루프/우회 복도 생성
    private HashSet<Vector2Int> AddExtraLoops(
        List<BoundsInt> room_list,
        HashSet<Vector2Int> floor,
        HashSet<Vector2Int> corridors,
        HashSet<(int, int)> mstEdges)
    {
        if (room_list.Count < 2)
            return corridors;

        // 모든 방 쌍의 엣지 계산
        var allEdges = BuildAllRoomEdges(room_list, floor);
        int added = 0;
        int attempted = 0;

        foreach (var (a, b, dist) in allEdges)
        {
            // 목표 개수 달성하면 종료
            if (added >= extraCorridorCount)
                break;

            // 너무 많이 시도하면 종료 (무한 루프 방지)
            if (attempted >= allEdges.Count)
                break;

            attempted++;

            // 이미 MST에 있는 엣지는 스킵
            int minIdx = Mathf.Min(a, b);
            int maxIdx = Mathf.Max(a, b);
            if (mstEdges.Contains((minIdx, maxIdx)))
                continue;

            // 거리가 너무 먼 엣지는 스킵
            if (dist > maxExtraCorridorDistance)
                continue;

            // 확률적으로 추가 (밀도 조절)
            if (Random.value > extraCorridorProbability)
                continue;

            // 복도 생성
            HashSet<Vector2Int> extraCorridor = CreateCorridorBetweenRooms(
                room_list[a],
                room_list[b],
                floor
            );

            // 맵 밖으로 나가는지 검사
            if (IsCorridorOutOfBounds(extraCorridor))
            {
                continue;
            }

            // 복도 추가
            corridors.UnionWith(extraCorridor);
            mstEdges.Add((minIdx, maxIdx)); // 중복 생성 방지
            added++;
        }

        return corridors;
    }
}
