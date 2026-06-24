CREATE PROCEDURE usp_GetProducts()
BEGIN
    SELECT
        Id,
        Name,
        Price
    FROM Products;
END
