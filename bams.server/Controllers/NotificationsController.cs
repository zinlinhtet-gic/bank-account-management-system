using bams.server.DTO.Common;
using bams.server.DTO.Notifications;
using bams.server.Messages;
using bams.server.Services.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace bams.server.Controllers;

/// <summary>The signed-in user's own notification inbox; every role has one, so no permission is required.</summary>
[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController(INotificationService notifications) : ControllerBase
{
    /// <summary>Lists the current user's unread notifications with the total unread count.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiMessageResponse<NotificationListResponse>>> GetUnreadAsync(CancellationToken cancellationToken)
    {
        var result = await notifications.GetUnreadAsync(cancellationToken);
        return Ok(ApiMessageResponse<NotificationListResponse>.FromCode(MessageCode.Success, result));
    }

    /// <summary>Marks one notification as read.</summary>
    [HttpPost("{notificationId:long}/read")]
    public async Task<ActionResult<ApiMessageResponse<bool>>> MarkReadAsync(long notificationId, CancellationToken cancellationToken)
    {
        await notifications.MarkReadAsync(notificationId, cancellationToken);
        return Ok(ApiMessageResponse<bool>.FromCode(MessageCode.Success, true));
    }

    /// <summary>Marks every notification of the current user as read.</summary>
    [HttpPost("read-all")]
    public async Task<ActionResult<ApiMessageResponse<bool>>> MarkAllReadAsync(CancellationToken cancellationToken)
    {
        await notifications.MarkAllReadAsync(cancellationToken);
        return Ok(ApiMessageResponse<bool>.FromCode(MessageCode.Success, true));
    }
}
