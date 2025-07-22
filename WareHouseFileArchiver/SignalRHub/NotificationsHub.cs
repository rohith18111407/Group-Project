using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace WareHouseFileArchiver.SignalRHub
{
    [Authorize]
    public class NotificationsHub : Hub
    {
        public async Task JoinGroup(string groupName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }

        public async Task LeaveGroup(string groupName)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userEmail = Context.User?.FindFirst(ClaimTypes.Email)?.Value;
            
            Console.WriteLine($"SignalR Client Connected: {Context.ConnectionId}, User: {userEmail ?? "Anonymous"}");
            
            // Add user to a general notifications group
            await Groups.AddToGroupAsync(Context.ConnectionId, "AllUsers");
            
            // Send a test message to confirm connection
            await Clients.Caller.SendAsync("TestConnection", "Connection established successfully!");
            
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userEmail = Context.User?.FindFirst(ClaimTypes.Email)?.Value;
            Console.WriteLine($"SignalR Client Disconnected: {Context.ConnectionId}, User: {userEmail ?? "Anonymous"}");
            
            if (exception != null)
            {
                Console.WriteLine($"Disconnect reason: {exception.Message}");
            }
            
            await base.OnDisconnectedAsync(exception);
        }

        // Test method to verify connection
        public async Task SendTestNotification()
        {
            await Clients.All.SendAsync("ReceiveNotification", new
            {
                Action = "Test Notification",
                Message = "This is a test notification from the server",
                Timestamp = DateTime.UtcNow
            });
        }
    }
}