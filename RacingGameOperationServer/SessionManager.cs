using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

public class SessionManager
{
    private ConcurrentDictionary<string, List<ClientInfo>> sessions;

    public SessionManager() 
    {
        sessions = new ConcurrentDictionary<string, List<ClientInfo>>();
    }

    public bool GetPlayersInSession(string sessionName, ref List<ClientInfo> clients)
    {
        bool ret = true;

        try
        {
            ret &= sessions.TryGetValue(sessionName, out clients);
        }
        catch (Exception)
        {
            ret = false;
        }
        
        return ret;
    }

    public bool GetPlayerInSession(string sessionName, string playerId, ref ClientInfo info)
    {
        bool ret = true;
        lock (sessions)
        {
            ret &= sessions.TryGetValue(sessionName, out List<ClientInfo> clients);
            try
            {
                if (ret)
                {
                    info = clients.FirstOrDefault(client => client.Id.Equals(playerId));
                }
            }
            catch (Exception)
            {
                ret = false;
            }
        }

        return ret;
    }

    public bool AddPlayerInSession(string sessionName, ClientInfo clientInfo)
    {
        bool ret = true;
        lock (sessions)
        {
            if (!sessions.ContainsKey(sessionName))
            {
                ret &= sessions.TryAdd(sessionName, new List<ClientInfo>());
            }

            try
            {
                sessions[sessionName].Add(clientInfo);
            }
            catch (Exception)
            {
                ret = false;
            }
        }
        return ret;
    }

    public bool RemovePlayerInSession(string sessionName, string playerId)
    {
        bool ret = true;

        lock (sessions)
        {
            if (!sessions.ContainsKey(sessionName))
            {
                return false;
            }

            try
            {
                ClientInfo find = sessions[sessionName].FirstOrDefault(x => x.Id.Equals(playerId));

                if (find != null)
                {
                    sessions[sessionName].Remove(find);
                }

                if (sessions[sessionName].Count <= 0)
                {
                    ret &= sessions.TryRemove(sessionName, out _);
                }
            }
            catch (Exception)
            {
                ret = false;
            }
        }

        return ret;
    }
}
