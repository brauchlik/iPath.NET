using iPath.Domain.Notificxations;
using Microsoft.AspNetCore.SignalR;

namespace iPath.API.Hubs;

public class NodeNotificationsHub : Hub
{
    public async Task SendNotification(NodeNofitication e){
        await Clients.All.SendAsync("NodeEvent", e);
    }
}


