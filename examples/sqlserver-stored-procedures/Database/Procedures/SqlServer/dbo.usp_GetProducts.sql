CREATE PROCEDURE dbo.usp_GetProducts
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        Id,
        Name,
        Price
    FROM dbo.Products;
END
