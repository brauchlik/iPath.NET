using iPath.Application.Contracts;
using iPath.Application.Features.Notifications;

namespace iPath.Application.Features.Notifications;

public class EmailNotificationPreview
    : IServiceRequestHtmlPreview
{
    public string Name => "email";

    public string CreatePreview(NotificationDto n, ServiceRequestDto sr)
    {
        var body = template.Replace("{title}", sr.Title);

        var desc = "";
        if (!string.IsNullOrEmpty(sr.Description.Text))
        {
            desc += sr.DescriptionHtml + "<br /><br />";
        }
        if (!string.IsNullOrEmpty(sr.Description.Questionnaire?.GeneratedText))
        {
            desc += sr.Description.Questionnaire?.GeneratedText + "<br /><br />";
        }
        body = body.Replace("{desc}", desc);


        // annotations
        var annoHtml = "";
        foreach (var a in sr.Annotations.OrderBy(x => x.CreatedOn))
        {
            annoHtml += $"""
<div class="comment_block">
    <div class="comment_sender">{a.Owner.Username} ({a.Owner.Email})</div>
    <div class="comment_text">{a.Data.Text}</div>
</div>
""";
        }
        body = body.Replace("{comments}", annoHtml);

        return body;
    }


    const string template = """
        <!DOCTYPE html>

        <html lang="en" xmlns="http://www.w3.org/1999/xhtml">
        <head>
            <meta charset="utf-8" />
            <title>Notification</title>
            <style>
                .cont {
                    width: 100vh;
                    border: 1px solid;
                    margin: 1em;
                }
                .title {
                    background-color: grey;
                    font-weight: bold;
                    padding: 8px;
                    border-bottom: 1px inset;
                }
                .sender {
                    background-color: grey;
                    padding: 8px;
                    border-bottom: 1px solid;
                }
                .desc{
                    padding: 1em;
                }
                .comments {
                    border-top: 1px inset;
                    margin-top: 1em;
                    padding: 1em;
                }
                .comment_block{
                    border-bottom: 1px inset;
                }
            </style>
        </head>
            <body>
                <div class="cont">
                    <div class="title">{title}</div>
                    <div class="sender">{sender}</div>
                    <div class="desc">{description}</div>

                    <div class="comments">
                        <div class="comments_title">Comments</div>
                        {comments}
                    </div>
                </div>
            </body>
        </html>
""";
}
