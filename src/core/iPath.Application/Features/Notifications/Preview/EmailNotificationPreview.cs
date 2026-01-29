using iPath.Application.Contracts;
using iPath.Application.Features.Notifications;

namespace iPath.Application.Features.Notifications;

public class EmailNotificationPreview
    : IServiceRequestHtmlPreview
{
    public string Name => "email";

    public string CreatePreview(NotificationDto n, ServiceRequestDto sr)
    {
        var title = sr.Title;
        if (!string.IsNullOrEmpty(sr.Description.CaseType))
        {
            title += ", " + sr.Description.CaseType;
        }
        if (sr.Description.BodySite is not null)
        {
            title += ", " + sr.Description.BodySite.ToString();
        }
        var body = template.Replace("{title}", title);

        var desc = "";
        if (!string.IsNullOrEmpty(sr.Description.Questionnaire?.GeneratedText))
        {
            desc += sr.Description.Questionnaire?.GeneratedText + "<br /><br />";
        }
        if (!string.IsNullOrEmpty(sr.Description.Text))
        {
            desc += sr.DescriptionHtml + "<br /><br />";
        }
        body = body.Replace("{description}", desc);

        string sender = sr.Owner.ToLongString() + ", " + sr.CreatedOn.ToShortDateString();
        body = body.Replace("{sender}", sender);

        // annotations
        var annoHtml = "";
        foreach (var a in sr.Annotations.OrderBy(x => x.CreatedOn))
        {
            annoHtml += $"""
<tr>
    <td class="comment_sender">{a.Owner.Username}, {a.CreatedOn.ToShortDateString()}<div class="comment_sender_email">({a.Owner.Email})</div></td>
    <td class="comment_text">{a.Data.Text}</td>
</tr>
""";
        }
        body = body.Replace("{comments}", annoHtml);

        return body;
    }


    string template => """
        <!DOCTYPE html>

        <html lang="en" xmlns="http://www.w3.org/1999/xhtml">
        <head>
            <meta charset="utf-8" />
            <title>Notification</title>
            <style>
                .cont {
                    width: 100%;
                    border: 1px solid;
                }
                .title {
                    background-color: lightgray;
                    font-weight: bold;
                    padding: 8px;
                    border-bottom: 1px inset;
                }
                .sender {
                    background-color: lightgray;
                    padding: 8px;
                    border-bottom: 1px solid;
                }
                .desc{
                    padding: 1em;
                }
                .comments {
                    border-top: 1px inset;
                    margin-top: 1em;
                }
                .comments_title {
                    padding: 8px;
                    font-weight: bold;
                    border-bottom: 1px solid;
                }
                .comments  table {
                    width: 100%;
                }
                .comments  tr {
                    border-bottom: 1px solid;
                }
                .comment_sender{
                    vertical-align: top;
                    width: 160px;
                    padding: 4px;
                    background-color: lightgray;
                }
                .comment_sender_email{
                    font-size: 0.8em;
                    font-style: italics;
                }
                .comment_text{
                    vertical-align: top;
                    padding: 4px;
                    padding-left: 1em;
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
                        <table class="comments_table">
                        {comments}
                        </table>
                    </div>
                </div>
            </body>
        </html>
""";
}
