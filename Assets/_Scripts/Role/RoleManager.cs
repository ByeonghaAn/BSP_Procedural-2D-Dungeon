using UnityEngine;
using System.Collections.Generic;

public class RoleManager : MonoBehaviour
{
    [SerializeField] private List<RoomRole> availableRoles;

    private Dictionary<string, RoomRole> roleDict;

    void Awake()
    {
        roleDict = new Dictionary<string, RoomRole>();
        foreach (var role in availableRoles)
        {
            roleDict[role.roleName] = role;
        }
    }

    public RoomRole GetRole(string roleName)
    {
        return roleDict.ContainsKey(roleName) ? roleDict[roleName] : null;
    }

    public List<RoomRole> GetAllRoles()
    {
        return availableRoles;
    }
}
