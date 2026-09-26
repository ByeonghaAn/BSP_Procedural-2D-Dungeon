using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public static class ProceduralGenerationAlgorithms
{
    public static HashSet<Vector2Int> RandomWalk(Vector2Int start_position, int walk_length)
    {
        HashSet<Vector2Int> path = new HashSet<Vector2Int>();
        path.Add(start_position);
        var previous_position = start_position;

        // Randomly walk the path
        for (int i = 0; i < walk_length; i++)
        {
            var next_position = previous_position + Directions2D.GetRandomDirections();
            path.Add(next_position);
            previous_position = next_position;
        }

        return path;
    }

    // use list (because ordered) to access the last position of the path
    public static List<Vector2Int> RandomWalkCorridor(Vector2Int start_position, int corridor_length)
    {
        List<Vector2Int> corridor = new List<Vector2Int>();
        var direction = Directions2D.GetRandomDirections();
        var current_position = start_position;
        corridor.Add(current_position);

        for (int i = 0; i < corridor_length; i++)
        {
            current_position = current_position + direction;
            corridor.Add(current_position);
        }

        return corridor;
    }

    // <BoundsInt> is truct in UnityEngine (UnityEngine.CoreModule)
    // Represents an axis aligned bounding box with all values as integers.
    // BinarySpacePartitioning is use to split the room
    public static List<BoundsInt> BinarySpacePartitioning(BoundsInt space_to_split, int min_width, int min_height)
    {
        // queue is data structure that implement FIFO (First In First Out)
        Queue<BoundsInt> rooms_queue = new Queue<BoundsInt>();
        List<BoundsInt> room_list = new List<BoundsInt>();
        rooms_queue.Enqueue(space_to_split);

        // if have rooms to split then do BSP
        while (rooms_queue.Count > 0)
        {
            var room = rooms_queue.Dequeue();
            if (room.size.x >= min_width && room.size.y >= min_height)
            {
                // random value generate value from 0 to 1
                if (UnityEngine.Random.value < 0.5f)
                {
                    // split horizontally if the room is bigger than min_height
                    if (room.size.y >= min_height * 2)
                    {
                        SplitHorizontally(min_height, rooms_queue, room);
                    }
                    // split vertically if the room is bigger than min_width
                    else if (room.size.x >= min_width * 2)
                    {
                        SplitVertically(min_width, rooms_queue, room);
                    }
                    // add a room if the area cannot be split
                    else if (room.size.x >= min_width && room.size.y >= min_height)
                    {
                        room_list.Add(room);
                    }
                }
                // change the direction of the split to vertically first to make it unique
                else
                {
                    // split vertically if the room is bigger than min_width
                    if (room.size.x >= min_width * 2)
                    {
                        SplitVertically(min_width, rooms_queue, room);
                    }
                    // split horizontally if the room is bigger than min_height
                    else if (room.size.y >= min_height * 2)
                    {
                        SplitHorizontally(min_height, rooms_queue, room);
                    }
                    // add a room if the area cannot be split
                    else if (room.size.x >= min_width && room.size.y >= min_height)
                    {
                        room_list.Add(room);
                    }
                }
            }
        }

        return room_list;
    }

    private static void SplitVertically(int min_width, Queue<BoundsInt> rooms_queue, BoundsInt room)
    {
        // range doesn't include the max value so room.size.x is minus 1
        var xSplit = Random.Range(1, room.size.x);
        BoundsInt room1 = new BoundsInt(room.min, new Vector3Int(xSplit, room.size.y, room.size.z));
        BoundsInt room2 = new BoundsInt(new Vector3Int(room.min.x + xSplit, room.min.y, room.min.z),
                          new Vector3Int(room.size.x - xSplit, room.size.y, room.size.z));

        rooms_queue.Enqueue(room1);
        rooms_queue.Enqueue(room2);
    }

    private static void SplitHorizontally(int min_height, Queue<BoundsInt> rooms_queue, BoundsInt room)
    {
        var ySplit = Random.Range(1, room.size.y);
        BoundsInt room1 = new BoundsInt(room.min, new Vector3Int(room.size.x, ySplit, room.size.z));
        BoundsInt room2 = new BoundsInt(new Vector3Int(room.min.x, room.min.y + ySplit, room.min.z),
                          new Vector3Int(room.size.x, room.size.y - ySplit, room.size.z));

        rooms_queue.Enqueue(room1);
        rooms_queue.Enqueue(room2);
    }

    /*
    // 의미 기반 BSP 방의 역할
    public enum RoomRole { Start, Boss, Shop, Treasure, Empty };
    // Room 구조체
    public readonly struct Room
    {
        public BoundsInt Bounds { get; }
        public RoomRole Role { get; }
        public Vector3Int Min => Bounds.min;          // 좌하단 좌표
        public Vector3Int Size => Bounds.size;        // 폭/높이/깊이
        public Vector3Int Max => Bounds.max;          // 우상단+1 좌표

        public Room(BoundsInt bounds, RoomRole role)
        {
            Bounds = bounds;
            Role = role;
        }
    }
    // 분할 후보를 저장하는 RoomLayoutCandidate 구조체
    private struct RoomLayoutCandidate
    {
        public List<Room> Rooms { get; }
        public float Fitness { get; }

        public RoomLayoutCandidate(List<Room> rooms, float fitness)
        {
            Rooms = rooms;
            Fitness = fitness;
        }
    }

    //의미 기반 BSP
    public static List<BoundsInt> SemanticBinarySpacePartitioning(
    BoundsInt space_to_split,
    int min_width,
    int min_height,
    float min_distance)
    {
        const int candidateCount = 7;
        List<RoomLayoutCandidate> candidates = new List<RoomLayoutCandidate>();

        // 방 후보 생성
        for (int i = 0; i < candidateCount; i++)
        {
            // 기본 BSP로 방 후보 생성
            List<BoundsInt> roomBounds = BinarySpacePartitioning(space_to_split, min_width, min_height);
            // 역할 할당
            List<Room> assignedRooms = AssignRoomRoles(roomBounds, space_to_split, min_width, min_height, min_distance);
            // 적합도 평가
            float fitness = FitnessFunc(assignedRooms, space_to_split, min_distance, min_width, min_height);
            // 후보 저장
            candidates.Add(new RoomLayoutCandidate(assignedRooms, fitness));
        }

        // 가장 높은 Fitness를 가진 후보 선택
        RoomLayoutCandidate bestCandidate = candidates[0];
        for (int i = 1; i < candidates.Count; i++)
        {
            if (candidates[i].Fitness > bestCandidate.Fitness)
            {
                bestCandidate = candidates[i];
            }
        }

        // 선택된 후보의 각 역할들이 어디있는지 좌표를 Debug로 출력***
        foreach (var room in bestCandidate.Rooms)
        {
            var b = room.Bounds;
            // BoundsInt에는 min, max, size 등이 있으며 ToString도 제공된다.
            if(room.Role == RoomRole.Start || room.Role == RoomRole.Boss || 
                room.Role == RoomRole.Shop || room.Role == RoomRole.Treasure)
            {
                Debug.LogFormat(
                "[SemanticBSP] Role={0} Min=({1},{2}) Max=({3},{4}) Size=({5},{6})",
                room.Role,
                b.min.x, b.min.y,
                b.max.x, b.max.y,
                b.size.x, b.size.y
                );
            }
        }

        // 선택된 후보를 List<Room>에서 List<BoundsInt>로 변환하고 리턴***
        List<BoundsInt> result = bestCandidate.Rooms.ConvertAll(r => r.Bounds);

        return result;
    }

    // 역할 할당 함수
    private static List<Room> AssignRoomRoles(
        List<BoundsInt> validRooms,
        BoundsInt mapBounds,
        int min_width,
        int min_height,
        float min_distance)
    {
        List<Room> rooms = new List<Room>();

        // Start 방 할당 (맵 가장자리 선호, 1개)
        BoundsInt startBounds = SelectRoomAtEdge(validRooms, mapBounds);
        validRooms.Remove(startBounds);
        Room startRoom = new Room(startBounds, RoomRole.Start);
        rooms.Add(startRoom);

        // Boss 방 할당 (맵 중앙 선호, Start와 min_distance 이상, 1개)
        BoundsInt bossBounds = SelectRoomAtCenter(validRooms, mapBounds, startBounds, min_distance);
        validRooms.Remove(bossBounds);
        rooms.Add(new Room(bossBounds, RoomRole.Boss));

        // Shop 방 할당 (Start와 min_distance 이상, 2개)
        List<BoundsInt> shopBounds = SelectRoomsWithDistanceConstraint(validRooms, startBounds, min_distance, 2);
        foreach (var shop in shopBounds)
        {
            validRooms.Remove(shop);
            rooms.Add(new Room(shop, RoomRole.Shop));
        }

        // Treasure 방 할당 (Start와 min_distance * 2 이내, 2개)
        List<BoundsInt> treasureBounds = SelectRoomsWithinDistance(validRooms, startBounds, min_distance * 2f, 2);
        foreach (var treasure in treasureBounds)
        {
            validRooms.Remove(treasure);
            rooms.Add(new Room(treasure, RoomRole.Treasure));
        }

        // Empty 방 할당 (나머지 모두)
        foreach (var emptyBounds in validRooms)
        {
            rooms.Add(new Room(emptyBounds, RoomRole.Empty));
        }

        return rooms;
    }

    // Room의 중심 좌표 계산 함수
    private static Vector2 GetRoomCenter(BoundsInt room)
    {
        return new Vector2(
            room.min.x + room.size.x / 2f,
            room.min.y + room.size.y / 2f
        );
    }

    // 맵 가장자리에 가까운 방 선택 (Start용 메서드)
    private static BoundsInt SelectRoomAtEdge(List<BoundsInt> rooms, BoundsInt mapBounds)
    {
        BoundsInt selectedRoom = rooms[0];
        float maxEdgeScore = float.MinValue;

        Vector2 mapCenter = new Vector2(
            mapBounds.min.x + mapBounds.size.x / 2f,
            mapBounds.min.y + mapBounds.size.y / 2f
        );

        foreach (var room in rooms)
        {
            Vector2 roomCenter = GetRoomCenter(room);
            float distanceToCenter = Vector2.Distance(roomCenter, mapCenter);

            if (distanceToCenter > maxEdgeScore)
            {
                maxEdgeScore = distanceToCenter;
                selectedRoom = room;
            }
        }

        return selectedRoom;
    }

    // 맵 중앙에 가까운 방 선택 (Start와 거리 제약 + 큰방 선호 + 맵 중심 선호, Boss용 메서드)
    private static BoundsInt SelectRoomAtCenter(
        List<BoundsInt> rooms,
        BoundsInt mapBounds,
        BoundsInt startRoom,
        float min_distance)
    {
        BoundsInt selectedRoom = rooms[0];
        float bestScore = 0f;

        Vector2 mapCenter = new Vector2(
            mapBounds.min.x + mapBounds.size.x / 2f,
            mapBounds.min.y + mapBounds.size.y / 2f
        );
        Vector2 startCenter = GetRoomCenter(startRoom);

        // 정규화를 위한 최대값 계산
        float maxDistanceToCenter = 0f;
        float maxRoomSize = 0f;

        foreach (var room in rooms)
        {
            Vector2 roomCenter = GetRoomCenter(room);
            float distanceToCenter = Vector2.Distance(roomCenter, mapCenter);
            float roomSize = room.size.x * room.size.y;

            if (distanceToCenter > maxDistanceToCenter)
                maxDistanceToCenter = distanceToCenter;
            if (roomSize > maxRoomSize)
                maxRoomSize = roomSize;
        }

        foreach (var room in rooms)
        {
            Vector2 roomCenter = GetRoomCenter(room);
            float distanceToCenter = Vector2.Distance(roomCenter, mapCenter);
            float distanceToStart = Vector2.Distance(roomCenter, startCenter);

            // Start와 최소 거리 제약 확인
            if (distanceToStart < min_distance)
                continue;

            // 다중 기준 점수 계산
            // 1. 중앙 근접도 (거리가 가까울수록 높은 점수, 0~1)
            float centerScore = 1f - (distanceToCenter / maxDistanceToCenter);

            // 2. 방 크기 (클수록 높은 점수, 0~1)
            float roomSize = room.size.x * room.size.y;
            float sizeScore = roomSize / maxRoomSize;

            // 가중치 적용 (중앙 선호: 0.5, 크기 선호: 0.5)
            float weightedScore = (centerScore * 0.5f) + (sizeScore * 0.5f);

            if (weightedScore > bestScore)
            {
                bestScore = weightedScore;
                selectedRoom = room;
            }
        }

        return selectedRoom;
    }

    // Start와 최소 거리 제약을 만족하는 방들 선택 (Shop용 메서드)
    private static List<BoundsInt> SelectRoomsWithDistanceConstraint(
        List<BoundsInt> rooms,
        BoundsInt referenceRoom,
        float min_distance,
        int count)
    {
        List<BoundsInt> selectedRooms = new List<BoundsInt>();
        Vector2 referenceCenter = GetRoomCenter(referenceRoom);

        List<BoundsInt> validCandidates = new List<BoundsInt>();
        foreach (var room in rooms)
        {
            Vector2 roomCenter = GetRoomCenter(room);
            float distance = Vector2.Distance(roomCenter, referenceCenter);

            if (distance >= min_distance)
            {
                validCandidates.Add(room);
            }
        }

        // count 개수만큼 랜덤 선택
        int selectCount = Mathf.Min(count, validCandidates.Count);
        for (int i = 0; i < selectCount; i++)
        {
            int randomIndex = Random.Range(0, validCandidates.Count);
            selectedRooms.Add(validCandidates[randomIndex]);
            validCandidates.RemoveAt(randomIndex);
        }

        return selectedRooms;
    }

    // Start와 최대 거리 이내에 있는 방들 선택 (Treasure용 메서드)
    private static List<BoundsInt> SelectRoomsWithinDistance(
        List<BoundsInt> rooms,
        BoundsInt referenceRoom,
        float max_distance,
        int count)
    {
        List<BoundsInt> selectedRooms = new List<BoundsInt>();
        Vector2 referenceCenter = GetRoomCenter(referenceRoom);

        // 최대 거리 이내의 후보 방들 필터링
        List<BoundsInt> validCandidates = new List<BoundsInt>();
        foreach (var room in rooms)
        {
            Vector2 roomCenter = GetRoomCenter(room);
            float distance = Vector2.Distance(roomCenter, referenceCenter);

            if (distance <= max_distance)
            {
                validCandidates.Add(room);
            }
        }

        // count 개수만큼 랜덤 선택
        int selectCount = Mathf.Min(count, validCandidates.Count);
        for (int i = 0; i < selectCount; i++)
        {
            int randomIndex = Random.Range(0, validCandidates.Count);
            selectedRooms.Add(validCandidates[randomIndex]);
            validCandidates.RemoveAt(randomIndex);
        }

        return selectedRooms;
    }

    // 적합도 평가 함수
    private static float FitnessFunc(
        List<Room> rooms, 
        BoundsInt mapBounds, 
        float min_distance, 
        int min_width, int min_height)
    {
        float totalScore = 0f;
        Room? startRoom = null;
        Room? bossRoom = null;
        List<Room> shopRooms = new List<Room>();
        List<Room> treasureRooms = new List<Room>();
        // 방 역할별 중요도
        const float startWeight = 2.5f;
        const float bossWeight = 2.5f;
        const float shopWeight = 1.0f;
        const float treasureWeight = 1.0f;

        foreach (var room in rooms)
        {
            switch (room.Role)
            {
                case RoomRole.Start:
                    startRoom = room;
                    break;
                case RoomRole.Boss:
                    bossRoom = room;
                    break;
                case RoomRole.Shop:
                    shopRooms.Add(room);
                    break;
                case RoomRole.Treasure:
                    treasureRooms.Add(room);
                    break;
            }
        }

        // 맵 중심
        Vector2 mapCenter = new Vector2(
            mapBounds.min.x + mapBounds.size.x / 2f,
            mapBounds.min.y + mapBounds.size.y / 2f
        );
        // 맵 내 가능한 최대거리(대각선 길이)
        float mapMaxDistance = Vector2.Distance(
            new Vector2(mapBounds.min.x, mapBounds.min.y),
            new Vector2(mapBounds.max.x, mapBounds.max.y)
        );

        // [Start] 방 위치 적합도
        if (startRoom.HasValue)
        {
            // Start 방 중심
            Vector2 startCenter = new Vector2(
                startRoom.Value.Bounds.min.x + startRoom.Value.Bounds.size.x / 2f,
                startRoom.Value.Bounds.min.y + startRoom.Value.Bounds.size.y / 2f
            );

            // 중심으로부터의 거리
            float distanceFromCenter = Vector2.Distance(startCenter, mapCenter);
            // 중심에서 가능한 최대 거리(중심에서 코너까지 대각선 거리)
            float maxDistanceFromCenter = Vector2.Distance(
                mapCenter,
                new Vector2(mapBounds.max.x, mapBounds.max.y)
            );

            if (maxDistanceFromCenter < 1e-5f) maxDistanceFromCenter = 1e-5f; // 안전장치(0분모 방지)
            
            // 중심에서 멀수록 1, 가까울수록 0
            float Score = Mathf.Clamp01(distanceFromCenter / maxDistanceFromCenter);

            // 역할 중요도(가중치) 적용
            totalScore += Score * startWeight;
        }
        // ------

        // [Boss] 방 위치 적합도
        if (bossRoom.HasValue)
        {
            // Boss 방 중심
            Vector2 bossCenter = new Vector2(
                bossRoom.Value.Bounds.min.x + bossRoom.Value.Bounds.size.x / 2f,
                bossRoom.Value.Bounds.min.y + bossRoom.Value.Bounds.size.y / 2f
            );

            // 중심으로부터의 거리
            float distanceFromCenter = Vector2.Distance(bossCenter, mapCenter);
            // 중심에서 가능한 최대 거리(중심에서 코너까지 대각선 거리)
            float maxDistanceFromCenter = Vector2.Distance(
                mapCenter,
                new Vector2(mapBounds.max.x, mapBounds.max.y)
            );

            if (maxDistanceFromCenter < 1e-5f) maxDistanceFromCenter = 1e-5f; // 안전장치(0분모 방지)

            // 중심에서 멀수록 0, 가까울수록 1
            float Score1 = Mathf.Clamp01(1f - (distanceFromCenter / maxDistanceFromCenter));

            // 역할 중요도(가중치) 적용
            totalScore += Score1 * bossWeight / 3f; // Boss 방은 제약조건 3개

            // [Boss] 방 면적 적합도
            float w = bossRoom.Value.Bounds.size.x;
            float h = bossRoom.Value.Bounds.size.y;
            float wMin = min_width;
            float hMin = min_height;
            float wMax = min_width * 2f;
            float hMax = min_height * 2f;

            // 방 면적이 최소일수록 0, 최대일수록 1
            float Score2 = Mathf.InverseLerp(wMin * hMin, wMax * hMax, w*h);

            // 역할 중요도(가중치) 적용
            totalScore += Score2 * bossWeight / 3f; // Boss 방은 제약조건 3개

            // [Boss] 역할 간 거리 제약 적합도

            // Start 방 중심
            Vector2 startCenter = new Vector2(
            startRoom.Value.Bounds.min.x + startRoom.Value.Bounds.size.x / 2f,
            startRoom.Value.Bounds.min.y + startRoom.Value.Bounds.size.y / 2f
            );

            // Boss, Start 사이의 거리
            float distanceFromStart = Vector2.Distance(startCenter, bossCenter);

            mapMaxDistance = Vector2.Distance(
            new Vector2(mapBounds.min.x, mapBounds.min.y),
            new Vector2(mapBounds.max.x, mapBounds.max.y)
            );
            if (mapMaxDistance < min_distance + 1e-5f) mapMaxDistance = min_distance + 1e-5f; // 안전장치

            float Score3 = 0f;
            if (distanceFromStart >= min_distance)
            {
                // 최소 거리 이상: distance가 min_distance에 가까울수록 0, mapMaxDistance에 가까울수록 1
                Score3 = Mathf.InverseLerp(min_distance, mapMaxDistance, distanceFromStart);
            }
            else
            {
                // 최소 거리 미만: distance가 min_distance에 가까울수록 0, 0에 가까울수록 -1
                float t = Mathf.InverseLerp(min_distance, 0f, distanceFromStart); // 0에 가까울수록 t↑
                Score3 = -1f * t; // 0..1 → 0..-1
            }

            // 역할 중요도(가중치) 적용
            totalScore += Score3 * bossWeight / 3f; // Boss 방은 제약조건 3개
        }
        // ------

        // [Shop] 역할 간 거리 제약 적합도
        if (startRoom.HasValue && shopRooms.Count > 0)
        {
            // Start 방 중심
            Vector2 startCenter = new Vector2(
                startRoom.Value.Bounds.min.x + startRoom.Value.Bounds.size.x / 2f,
                startRoom.Value.Bounds.min.y + startRoom.Value.Bounds.size.y / 2f
            );

            foreach (var shop in shopRooms)
            {
                // Shop 방 중심
                Vector2 shopCenter = new Vector2(
                    shop.Bounds.min.x + shop.Bounds.size.x / 2f,
                    shop.Bounds.min.y + shop.Bounds.size.y / 2f
                );

                float distance = Vector2.Distance(startCenter, shopCenter);
                float Score;
                if (distance >= min_distance)
                {
                    // 최소 거리 이상: 점수 1
                    Score = 1f;
                }
                else
                {
                    // 최소 거리 미만: distance가 min_distance에 가까울수록 0, 0에 가까울수록 -1
                    float t = Mathf.InverseLerp(min_distance, 0f, distance); // 0에 가까울수록 t↑
                    Score = -1f * t; // 0..1 → 0..-1
                }

                // 역할 중요도(가중치) 적용
                totalScore += Score * shopWeight;
            }
        }
        // ------

        // [Treasure] 역할 간 거리 제약 적합도
        if (startRoom.HasValue && treasureRooms.Count > 0)
        {
            // Start 방 중심
            Vector2 startCenter = new Vector2(
                startRoom.Value.Bounds.min.x + startRoom.Value.Bounds.size.x / 2f,
                startRoom.Value.Bounds.min.y + startRoom.Value.Bounds.size.y / 2f
            );

            float maxD = min_distance * 2f;

            mapMaxDistance = Vector2.Distance(
            new Vector2(mapBounds.min.x, mapBounds.min.y),
            new Vector2(mapBounds.max.x, mapBounds.max.y)
            );
            if (mapMaxDistance < maxD + 1e-5f) mapMaxDistance = maxD + 1e-5f;  // 안전장치

            foreach (var treasure in treasureRooms)
            {
                // Treasure 방 중심
                Vector2 tCenter = new Vector2(
                    treasure.Bounds.min.x + treasure.Bounds.size.x / 2f,
                    treasure.Bounds.min.y + treasure.Bounds.size.y / 2f
                );

                float distance = Vector2.Distance(startCenter, tCenter);

                float Score;
                if (distance <= maxD)
                {
                    // 제한 이내: 점수 1
                    Score = 1f;
                }
                else
                {
                    // 제한 초과: distance가 maxD에 가까울수록 0, mapMaxDistance에 가까울수록 -1 
                    float t = Mathf.InverseLerp(maxD, mapMaxDistance, distance); // mapMaxDistance에 가까울수록 t↑
                    Score = -1f * t; // 0..1 → 0..-1
                }

                // 역할 중요도(가중치) 적용
                totalScore += Score * treasureWeight;
            }
        }
        // ------

        return totalScore;
    }
    */
}