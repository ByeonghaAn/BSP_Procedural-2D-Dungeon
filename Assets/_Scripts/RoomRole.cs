using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

[System.Serializable]
public class DistanceConstraint
{
    public float DistanceWeight;
    public string targetRoleName;
    public float minDistance;
    public float maxDistance;
}

[CreateAssetMenu(fileName = "New Room Role", menuName = "BSP/Room Role")]
public class RoomRole : ScriptableObject
{
    [Header("기본 정보")]
    public string roleName;

    [Header("하드 제약 (Hard Constraints)")]
    public int Count = 1; // -1일 경우 제약없음
    public int minRoomWidth = 10, minRoomHeight = 10; // 방 최소 크기
    public int maxRoomWidth = 20, maxRoomHeight = 20; // 방 최대 크기

    [Header("소프트 제약 (Soft Constraints / Preferences)")]
    [Header("중앙 선호")]
    public float CenterWeight = 0.0f;

    [Header("가장자리 선호")]
    public float EdgeWeight = 0.0f;

    [Header("선호 면적")]
    public float SizeWeight = 0.0f;
    public int SizeWidth = 20, SizeHeight = 20; // 선호 면적

    [Header("역할 간 거리 제약")]
    public DistanceConstraint[] distanceConstraints; // 여러 역할과의 거리 제약

    [Header("역할 중요도 (Role Weight)")]
    public float RoleWeight = 0.0f;

    [Header("역할 배치 우선도 (Priority)")]
    public int Priority = 0; // 작을수록 우선순위 높음 (0보다 작은 경우, 예외로 역할 배치X)

    // 역할 적합도 계산
    public float CalculateRoleFitness(
        BoundsInt nodeBounds,
        BoundsInt mapBounds,
        Dictionary<string, AssignedRoleInfo> assignedRoles)
    {
        float totalFitness = 0f;

        // 노드/맵 중심 계산
        Vector3 nodeCenter = nodeBounds.center;
        Vector3 mapCenter = mapBounds.center;
        float distToCenter = Vector3.Distance(nodeCenter, mapCenter);
        float maxDistToCenter = new Vector3(mapBounds.size.x, mapBounds.size.y, 0).magnitude * 0.5f;
        if (maxDistToCenter <= 0f) maxDistToCenter = 0.0001f; // 0 나누기 방지

        // 1. 중앙 선호 (CenterWeight가 0이면 영향 없음)
        // 중앙에 가까울수록 1, 먼 곳일수록 0
        float centrality = 1f - Mathf.Clamp01(distToCenter / maxDistToCenter);
        float centerScore = centrality * CenterWeight;
        totalFitness += centerScore;

        // 2. 가장자리 선호 (EdgeWeight가 0이면 영향 없음)
        // 중앙에 가까울수록 0, 가장자리에 가까울수록 1
        float edgeFactor = Mathf.Clamp01(distToCenter / maxDistToCenter);
        float edgeScore = edgeFactor * EdgeWeight;
        totalFitness += edgeScore;

        // 3. 선호 면적 (SizeWeight가 0이면 영향 없음)
        // 선호 너비/높이(SizeWidth/SizeHeight)와의 차이가 작을수록 높은 점수
        float widthDiff = Mathf.Abs(nodeBounds.size.x - SizeWidth);
        float heightDiff = Mathf.Abs(nodeBounds.size.y - SizeHeight);
        float sizePenalty = widthDiff + heightDiff;          // 차이 합
        float sizeScore = 1f / (1f + sizePenalty);           // 0~1, 차이가 클수록 0에 가까워짐
        sizeScore *= SizeWeight;
        totalFitness += sizeScore;

        // 4. 역할 간 거리 제약 (distanceConstraints가 없으면 스킵, DistanceWeight가 0이면 영향 없음)
        if (distanceConstraints != null && assignedRoles != null)
        {
            foreach (var constraint in distanceConstraints)
            {
                if (!assignedRoles.TryGetValue(constraint.targetRoleName, out AssignedRoleInfo targetInfo))
                    continue;

                if (targetInfo.Rooms == null || targetInfo.Rooms.Count == 0)
                    continue;

                float sumDistScore = 0f;
                int count = 0;

                foreach (var assignedRoom in targetInfo.Rooms)
                {
                    Vector3 targetCenter = assignedRoom.Bounds.center;
                    float dist = Vector3.Distance(nodeCenter, targetCenter);

                    float distScore = 0f;

                    // min~max 범위 안이면 무조건 1점
                    if (dist >= constraint.minDistance && dist <= constraint.maxDistance)
                    {
                        distScore = 1f;
                    }
                    // 범위를 벗어나면: 얼마나 벗어났는지에 따라 페널티 (-1 ~ 0)
                    else
                    {
                        float penalty;
                        if (dist < constraint.minDistance)
                            penalty = (constraint.minDistance - dist) / Mathf.Max(constraint.minDistance, 0.0001f);
                        else
                            penalty = (dist - constraint.maxDistance) / Mathf.Max(constraint.maxDistance, 0.0001f);

                        distScore = -Mathf.Clamp01(penalty); // -1 ~ 0
                    }

                    sumDistScore += distScore;
                    count++;
                }

                // 같은 역할이 여러 개일 경우 평균 점수 사용
                float avgDistScore = (count > 0) ? (sumDistScore / count) : 0f;

                // 거리 점수에 DistanceWeight 반영
                totalFitness += avgDistScore * constraint.DistanceWeight;
            }
        }

        // 5. 역할 전체 가중치(RoleWeight) 반영
        totalFitness *= RoleWeight;

        return totalFitness;
    }
}

public class AssignedRoomInfo
{
    public BoundsInt Bounds;
    public float Fitness;

    public AssignedRoomInfo(BoundsInt bounds, float fitness)
    {
        Bounds = bounds;
        Fitness = fitness;
    }
}

public class AssignedRoleInfo
{
    // 이 역할로 배치된 방들 + 각 방의 Fitness
    public List<AssignedRoomInfo> Rooms = new List<AssignedRoomInfo>();
}

public static class RoleAssignmentHelper
{
    // 역할에 방 추가 (Bounds + Fitness)
    public static void AddRole(this Dictionary<string, AssignedRoleInfo> dict,
                               string roleName, BoundsInt bounds, float fitness)
    {
        if (!dict.TryGetValue(roleName, out AssignedRoleInfo roleInfo))
        {
            roleInfo = new AssignedRoleInfo();
            dict[roleName] = roleInfo;
        }

        roleInfo.Rooms.Add(new AssignedRoomInfo(bounds, fitness));
    }

    // 특정 역할의 현재 개수
    public static int GetRoleCount(this Dictionary<string, AssignedRoleInfo> dict,
                                   string roleName)
    {
        return dict.TryGetValue(roleName, out AssignedRoleInfo roleInfo)
            ? roleInfo.Rooms.Count
            : 0;
    }

    // 특정 역할의 i번째 방 정보 가져오기
    public static AssignedRoomInfo GetRoomInfo(this Dictionary<string, AssignedRoleInfo> dict,
                                               string roleName, int index)
    {
        if (dict.TryGetValue(roleName, out AssignedRoleInfo roleInfo) &&
            index >= 0 && index < roleInfo.Rooms.Count)
        {
            return roleInfo.Rooms[index];
        }
        return null;
    }

    // Count 하드제약을 만족하는지 확인
    public static bool MeetsCountConstraints(this Dictionary<string, AssignedRoleInfo> dict,
                                             List<RoomRole> roles)
    {
        foreach (var role in roles)
        {
            if (role.Count == -1) // Count = -1 이면 제약 없음
                continue;

            int currentCount = dict.GetRoleCount(role.roleName);
            if (currentCount != role.Count)
                return false;
        }
        return true;
    }
}

public static class RoleAssigner
{
    /// <summary>
    /// 노드 리스트와 역할 리스트가 주어졌을 때,
    /// 1) Priority(낮을수록 먼저) 순으로 역할을 선택하고
    /// 2) 각 역할마다 Count 개수만큼, 아직 비어 있는 노드 중에서 Fitness가 높은 순으로 배치한다.
    /// 3) Count = -1인 역할은 남아 있는 모든 비어 있는 노드에 배치한다.
    /// 4) 한 노드에는 하나의 역할만 배치된다.
    /// 5) 노드 수가 부족하면 가능한 만큼만 배치하고 나머지 역할은 스킵한다.
    /// 결과는 역할 이름별로 배치 정보가 들어 있는 Dictionary<string, AssignedRoleInfo>를 반환한다.
    /// </summary>
    public static Dictionary<string, AssignedRoleInfo> AssignRoles(
        List<BoundsInt> nodes,
        BoundsInt mapBounds,
        List<RoomRole> roles)
    {
        var assignedByRole = new Dictionary<string, AssignedRoleInfo>();

        if (nodes == null || nodes.Count == 0 || roles == null || roles.Count == 0)
            return assignedByRole;

        // 노드별로 이미 사용됐는지 표시
        // false = 아직 배치되지 않음, true = 이미 어떤 역할이 사용함
        bool[] nodeUsed = new bool[nodes.Count];

        // 1. Priority 기준으로 역할 정렬 (작을수록 먼저, Priority < 0 은 아예 배치X)
        var orderedRoles = new List<RoomRole>();
        foreach (var role in roles)
        {
            if (role.Priority < 0)
                continue; // 배치 대상 아님
            orderedRoles.Add(role);
        }
        orderedRoles.Sort((a, b) => a.Priority.CompareTo(b.Priority));

        // 2. 역할 순서대로, 각 역할을 Count 개수만큼 배치 시도
        foreach (var role in orderedRoles)
        {
            int remaining = role.Count;

            // Count = -1이면: 아직 역할이 배치되지 않은 모든 노드에 이 역할을 배치
            if (remaining < 0)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (nodeUsed[i])
                        continue;

                    BoundsInt node = nodes[i];

                    if (node.size.x < role.minRoomWidth || node.size.y < role.minRoomHeight)
                    {
                        continue; // 최소 방 크기를 만족하지 못하는 노드는 스킵
                    }

                    float fitness = role.CalculateRoleFitness(node, mapBounds, assignedByRole);

                    // 상태 업데이트
                    assignedByRole.AddRole(role.roleName, node, fitness);
                    nodeUsed[i] = true;
                }

                // 이 역할에 대한 처리는 끝났으니 다음 역할로
                continue;
            }

            // Count = 0이면 배치 안 함
            if (remaining == 0)
                continue;

            // 이 역할이 배치될 수 있는 후보 노드들의 Fitness를 미리 계산
            var candidates = new List<(int nodeIndex, float fitness)>();

            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodeUsed[i])
                    continue; // 이미 다른 역할이 사용한 노드

                BoundsInt node = nodes[i];

                if (node.size.x < role.minRoomWidth || node.size.y < role.minRoomHeight)
                {
                    continue; // 최소 방 크기를 만족하지 못하는 노드는 제외
                }

                // 이 노드에 대해 이 역할의 Fitness 계산
                float fitness = role.CalculateRoleFitness(node, mapBounds, assignedByRole);

                candidates.Add((i, fitness));
            }

            // 후보가 하나도 없으면 이 역할은 더 이상 배치 불가
            if (candidates.Count == 0)
                continue;

            // Fitness 높은 순으로 정렬
            candidates.Sort((a, b) => b.fitness.CompareTo(a.fitness));

            // 상위에서부터 Count 개수만큼, 아직 역할이 없는 노드에 배치
            foreach (var (nodeIndex, fitness) in candidates)
            {
                if (remaining <= 0)
                    break;

                if (nodeUsed[nodeIndex])
                    continue; // 다른 역할이 먼저 가져간 경우

                // 이 노드에 역할 배치
                var nodeBounds = nodes[nodeIndex];

                // 상태 업데이트: 역할별/노드별
                assignedByRole.AddRole(role.roleName, nodeBounds, fitness);
                nodeUsed[nodeIndex] = true;

                remaining--;
            }

            // 노드 수가 부족하면 여기서 더 이상 배치되지 않고 끝남
        }

        return assignedByRole;
    }
}

public class BspNode
{
    public BoundsInt Bounds;
    public BspNode Left;
    public BspNode Right;
    public bool IsLeaf => Left == null && Right == null;

    public BspNode(BoundsInt bounds)
    {
        Bounds = bounds;
    }
}

public class SemanticBspGenerator
{
    private List<RoomRole> _roles;
    private BoundsInt _mapBounds;

    public SemanticBspGenerator(BoundsInt mapBounds, List<RoomRole> roles)
    {
        _mapBounds = mapBounds;
        _roles = roles;
    }

    /// <summary>
    /// 의미 기반 BSP:
    /// space_to_split을 루트로 시작해 재귀적으로 분할하면서,
    /// 각 분할 단계마다 7개 후보 중 Fitness가 가장 높은 것을 선택한다.
    /// 최종적으로 BSP 트리의 리프 노드들을 반환하면서, 역할 배치 정보와 전체 Fitness를 출력한다.
    /// </summary>
    public List<BoundsInt> SemanticBinarySpacePartitioning(
        BoundsInt space_to_split, int min_width, int min_height)
    {
        // 루트 노드로 시작
        var root = new BspNode(space_to_split);

        // 분할 대기 중인 리프 노드들
        var openLeaves = new List<BspNode> { root };

        // 안전장치 추가
        int splitCount = 0;
        int MAX_SPLITS = 600;
        // 분할을 계속 진행
        while (splitCount < MAX_SPLITS)
        {
            // 아직 분할 가능한 노드 찾기
            BspNode nodeToSplit = null;
            foreach (var leaf in openLeaves)
            {
                // 분할 가능한 조건: 너비/높이가 min_width*2, min_height*2 이상
                if (leaf.Bounds.size.x >= min_width * 2 || leaf.Bounds.size.y >= min_height * 2)
                {
                    nodeToSplit = leaf;
                    break;
                }
            }

            // 더 이상 분할할 노드가 없으면 종료
            if (nodeToSplit == null)
                break;

            BoundsInt bounds = nodeToSplit.Bounds;

            // 7개 분할 후보 생성
            var candidates = GenerateSplitCandidates(bounds, min_width, min_height, count: 7);

            if (candidates.Count == 0)
            {
                // 분할 불가능하면 이 노드는 더 이상 분할하지 않음
                openLeaves.Remove(nodeToSplit);
                continue;
            }

            float bestFitness = float.NegativeInfinity;
            (BoundsInt left, BoundsInt right) bestSplit = default;

            // 각 후보에 대해 AssignRoles 실행 후 Fitness 계산
            foreach (var split in candidates)
            {
                // 현재 분할 노드를 제외한 나머지 리프들
                var candidateNodes = new List<BoundsInt>();

                foreach (var leaf in openLeaves)
                {
                    if (leaf != nodeToSplit) // 현재 분할 대상 제외
                    {
                        candidateNodes.Add(leaf.Bounds);
                    }
                }

                // 분할 후보의 두 자식 추가
                candidateNodes.Add(split.left);
                candidateNodes.Add(split.right);

                var assignedByRole = RoleAssigner.AssignRoles(candidateNodes, _mapBounds, _roles);

                float totalFitness = 0f;
                foreach (var kvp in assignedByRole)
                {
                    foreach (var room in kvp.Value.Rooms)
                    {
                        totalFitness += room.Fitness;
                    }
                }

                if (totalFitness > bestFitness)
                {
                    bestFitness = totalFitness;
                    bestSplit = split;
                }
            }

            // 가장 좋은 후보로 실제 분할
            if (bestFitness > float.NegativeInfinity)
            {
                var leftNode = new BspNode(bestSplit.left);
                var rightNode = new BspNode(bestSplit.right);

                nodeToSplit.Left = leftNode;
                nodeToSplit.Right = rightNode;

                // 리프 리스트 갱신: nodeToSplit 제거, 두 자식 추가
                openLeaves.Remove(nodeToSplit);
                // 최소 방 크기 체크
                if (leftNode.Bounds.size.x >= min_width && leftNode.Bounds.size.y >= min_height)
                {
                    openLeaves.Add(leftNode);
                }
                if (rightNode.Bounds.size.x >= min_width && rightNode.Bounds.size.y >= min_height)
                {
                    openLeaves.Add(rightNode);
                }

                splitCount++;
            }
            else
            {
                // 분할 후보는 있지만 Fitness가 모두 -∞인 경우
                openLeaves.Remove(nodeToSplit);
            }
        }

        // 최종 리프 노드들
        List<BoundsInt> leafBounds = new List<BoundsInt>();
        foreach (var leaf in openLeaves)
        {
            if (leaf.Bounds.size.x >= min_width && leaf.Bounds.size.y >= min_height)
            {
                leafBounds.Add(leaf.Bounds);
            }
        }

        // 최종 리프들에 대해 역할 배치 수행 (전체 맵 기준)
        var finalAssignedByRole = RoleAssigner.AssignRoles(leafBounds, _mapBounds, _roles);

        // 역할별로 배치된 방 정보와 Fitness 출력
        float totalMapFitness = 0f;

        foreach (var kvp in finalAssignedByRole)
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

        return leafBounds;
    }

    /// <summary>
    /// 주어진 Bounds를 수평/수직으로 나누는 분할 후보들을 생성한다.
    /// </summary>
    private List<(BoundsInt left, BoundsInt right)> GenerateSplitCandidates(
        BoundsInt bounds, int min_width, int min_height, int count)
    {
        var list = new List<(BoundsInt left, BoundsInt right)>();

        // 분할 가능 여부 미리 체크
        bool canSplitVertical = bounds.size.x >= min_width * 2;
        bool canSplitHorizontal = bounds.size.y >= min_height * 2;

        // 둘 다 분할 불가능하면 빈 리스트 반환
        if (!canSplitVertical && !canSplitHorizontal)
            return list;

        int trials = count;
        for (int t = 0; t < trials && list.Count < count; t++)
        {
            bool vertical;

            // 분할 방향 결정
            if (canSplitVertical && !canSplitHorizontal)
            {
                vertical = true; // 수직 분할만 가능
            }
            else if (!canSplitVertical && canSplitHorizontal)
            {
                vertical = false; // 수평 분할만 가능
            }
            else
            {
                vertical = Random.value > 0.5f; // 둘 다 가능하면 랜덤
            }

            if (vertical)
            {
                // range doesn't include the max value so room.size.x is minus 1
                var xSplit = Random.Range(1, bounds.size.x);

                var left = new BoundsInt(bounds.xMin, bounds.yMin, bounds.zMin,
                                         xSplit, bounds.size.y, bounds.size.z);

                var right = new BoundsInt(bounds.xMin + xSplit, bounds.yMin, bounds.zMin,
                                          bounds.size.x - xSplit, bounds.size.y, bounds.size.z);

                list.Add((left, right));
            }
            else
            {
                var ySplit = Random.Range(1, bounds.size.y);

                var bottom = new BoundsInt(bounds.xMin, bounds.yMin, bounds.zMin,
                                           bounds.size.x, ySplit, bounds.size.z);

                var top = new BoundsInt(bounds.xMin, bounds.yMin + ySplit, bounds.zMin,
                                        bounds.size.x, bounds.size.y - ySplit, bounds.size.z);

                list.Add((bottom, top));
            }
        }

        return list;
    }
}