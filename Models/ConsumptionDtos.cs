namespace FormBuilder.API.Models
{
    public class ConsolidatedConsumption
    {
        public string ProductName { get; set; } = string.Empty;
        public decimal TotalQuantity { get; set; }
        public string Unit { get; set; } = string.Empty;
        public int FormCount { get; set; }
        public List<string> Areas { get; set; } = new();
        public DateTime? LastDate { get; set; }
    }
    
    public class ConsumptionDetail
    {
        public DateTime Date { get; set; }
        public string FormName { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string? BatchCode { get; set; }
        public string Responsible { get; set; } = string.Empty;
    }
    
    public class ConsumptionStats
    {
        public int TotalProducts { get; set; }
        public decimal TotalConsumption { get; set; }
        public int TotalForms { get; set; }
        public int TotalAreas { get; set; }
    }
    
    public class ConsumptionFilters
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Product { get; set; }
        public string? Area { get; set; }
        public string GroupBy { get; set; } = "product"; // "product", "area", "date", "template"
    }
    
    public class ProductSummary
    {
        public string Name { get; set; } = string.Empty;
        public decimal TotalConsumption { get; set; }
        public string Unit { get; set; } = string.Empty;
    }
    
    public class AreaSummary
    {
        public string Name { get; set; } = string.Empty;
        public int ProductCount { get; set; }
    }
    
    public class PeriodComparison
    {
        public decimal Period1Total { get; set; }
        public decimal Period2Total { get; set; }
        public decimal Difference { get; set; }
        public decimal PercentageChange { get; set; }
        public List<ProductComparison> Products { get; set; } = new();
    }
    
    public class ProductComparison
    {
        public string Name { get; set; } = string.Empty;
        public decimal Period1 { get; set; }
        public decimal Period2 { get; set; }
        public decimal Difference { get; set; }
        public decimal PercentageChange { get; set; }
    }
    
    public class ComparePeriodRequest
    {
        public PeriodDates Period1 { get; set; } = new();
        public PeriodDates Period2 { get; set; } = new();
    }
    
    public class PeriodDates
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }

    public class ExportSectionsRequest
    {
        public List<ExportSectionItem> Sections { get; set; } = new();
    }

    public class ExportSectionItem
    {
        public int FormID { get; set; }
        public string? TemplateName { get; set; }
        public string? TemplateCode { get; set; }
        public string? Area { get; set; }
        public string? FilledBy { get; set; }
        public string? CreatedAt { get; set; }
        public string? SectionTitle { get; set; }
        public string? SectionType { get; set; }
        public List<string> Columns { get; set; } = new();
        public List<Dictionary<string, object>> Rows { get; set; } = new();
    }
}
