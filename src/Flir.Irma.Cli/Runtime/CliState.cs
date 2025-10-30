using Flir.Irma.Cli.Configuration;

namespace Flir.Irma.Cli.Runtime;

internal sealed class CliState
{
    public CliState(IDictionary<string, string> defaults, AuthTicket? authTicket, SessionState sessionState)
    {
        Defaults = new Dictionary<string, string>(defaults, StringComparer.OrdinalIgnoreCase);
        AuthTicket = authTicket;
        Session = sessionState;
    }

    public Dictionary<string, string> Defaults { get; }

    public AuthTicket? AuthTicket { get; private set; }

    public SessionState Session { get; }

    public bool DefaultsModified { get; private set; }
    public bool AuthModified { get; private set; }
    public bool AuthCleared { get; private set; }
    public bool SessionModified { get; private set; }

    public void SetDefault(string key, string value)
    {
        Defaults[key] = value;
        DefaultsModified = true;
    }

    public bool ClearDefault(string key)
    {
        var removed = Defaults.Remove(key);
        if (removed)
        {
            DefaultsModified = true;
        }

        return removed;
    }

    public void SetAuth(AuthTicket ticket)
    {
        AuthTicket = ticket;
        AuthModified = true;
        AuthCleared = false;
    }

    public void ClearAuth()
    {
        AuthTicket = null;
        AuthModified = false;
        AuthCleared = true;
    }

    public void SetCurrentConversation(Guid conversationId)
    {
        Session.CurrentConversationId = conversationId;
        if (!Session.RecentConversations.Contains(conversationId))
        {
            Session.RecentConversations.Insert(0, conversationId);
            if (Session.RecentConversations.Count > 10)
            {
                Session.RecentConversations.RemoveAt(Session.RecentConversations.Count - 1);
            }
        }

        SessionModified = true;
    }
}
