using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace EduManageLms.Api.Application;

public static class AdminReportPdfBuilder
{
    public static byte[] Build(
        AdminReportDto report,
        string filterDescription,
        DateTime generatedAt)
    {
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(style => style.FontSize(10));

                page.Header().Column(header =>
                {
                    header.Item()
                        .Text("BÁO CÁO ĐÀO TẠO EDUMANAGE LMS")
                        .Bold()
                        .FontSize(18)
                        .FontColor(Colors.Blue.Darken2);
                    header.Item()
                        .PaddingTop(4)
                        .Text(filterDescription)
                        .FontColor(Colors.Grey.Darken1);
                    header.Item()
                        .Text($"Ngày tạo: {generatedAt:dd/MM/yyyy HH:mm}")
                        .FontSize(9)
                        .FontColor(Colors.Grey.Darken1);
                });

                page.Content()
                    .PaddingVertical(16)
                    .Column(column =>
                    {
                        column.Spacing(14);
                        column.Item().Element(container =>
                            ComposeCards(container, report.Cards));
                        column.Item().Element(container =>
                            ComposeTable(
                                container,
                                "Sinh viên theo khoa",
                                report.StudentsByFaculty));
                        column.Item().Element(container =>
                            ComposeTable(
                                container,
                                "Trạng thái bảng điểm",
                                report.GradeStatus));
                        column.Item().Element(container =>
                            ComposeTable(
                                container,
                                "Trạng thái học tập",
                                report.LearningStatus));
                        column.Item().Element(container =>
                            ComposeTable(
                                container,
                                "Mức đạt CLO trung bình (%)",
                                report.CloAchievement));
                        column.Item().Element(container =>
                            ComposeActivities(
                                container,
                                report.RecentActivities));
                    });

                page.Footer()
                    .AlignCenter()
                    .Text(text =>
                    {
                        text.Span("Trang ");
                        text.CurrentPageNumber();
                        text.Span("/");
                        text.TotalPages();
                    });
            });
        }).GeneratePdf();
    }

    private static void ComposeCards(
        IContainer container,
        IReadOnlyCollection<DashboardCardDto> cards)
    {
        container.Column(column =>
        {
            column.Item().Text("Tổng quan").Bold().FontSize(13);
            column.Item().PaddingTop(6).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.ConstantColumn(90);
                });
                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("Chỉ số");
                    header.Cell().Element(HeaderCell).Text("Giá trị");
                });
                foreach (var card in cards)
                {
                    table.Cell().Element(BodyCell).Text(card.Label);
                    table.Cell().Element(BodyCell)
                        .AlignRight()
                        .Text(card.Value?.ToString() ?? string.Empty)
                        .Bold();
                }
            });
        });
    }

    private static void ComposeTable(
        IContainer container,
        string title,
        IReadOnlyCollection<ChartItemDto> items)
    {
        container.Column(column =>
        {
            column.Item().Text(title).Bold().FontSize(13);
            column.Item().PaddingTop(6).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.ConstantColumn(90);
                });
                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("Nội dung");
                    header.Cell().Element(HeaderCell).Text("Giá trị");
                });
                foreach (var item in items)
                {
                    table.Cell().Element(BodyCell).Text(item.Label);
                    table.Cell().Element(BodyCell)
                        .AlignRight()
                        .Text(item.Value.ToString("0.##"));
                }
                if (items.Count == 0)
                {
                    table.Cell().ColumnSpan(2)
                        .Element(BodyCell)
                        .Text("Chưa có dữ liệu.");
                }
            });
        });
    }

    private static void ComposeActivities(
        IContainer container,
        IReadOnlyCollection<ActivityDto> activities)
    {
        container.Column(column =>
        {
            column.Item().Text("Hoạt động gần đây").Bold().FontSize(13);
            foreach (var activity in activities)
            {
                column.Item()
                    .PaddingVertical(5)
                    .BorderBottom(0.5f)
                    .BorderColor(Colors.Grey.Lighten2)
                    .Row(row =>
                    {
                        row.RelativeItem().Column(content =>
                        {
                            content.Item().Text(activity.Title).SemiBold();
                            content.Item()
                                .Text(activity.Description)
                                .FontSize(9)
                                .FontColor(Colors.Grey.Darken1);
                        });
                        row.ConstantItem(85)
                            .AlignRight()
                            .Text(activity.Time)
                            .FontSize(9);
                    });
            }
        });
    }

    private static IContainer HeaderCell(IContainer container) =>
        container
            .Background(Colors.Blue.Lighten4)
            .Border(0.5f)
            .BorderColor(Colors.Grey.Lighten1)
            .Padding(6)
            .DefaultTextStyle(style => style.SemiBold());

    private static IContainer BodyCell(IContainer container) =>
        container
            .BorderBottom(0.5f)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(6);
}
