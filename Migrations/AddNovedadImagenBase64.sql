IF COL_LENGTH('dbo.Novedades', 'ImagenBase64') IS NULL
BEGIN
    ALTER TABLE dbo.Novedades
    ADD ImagenBase64 NVARCHAR(MAX) NULL;
END