SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;

USE [StudentSMS];
GO

/* A plain UNIQUE constraint treats NULL as equal, so the second payment without a
   receipt number (or gateway payment id / idempotency key) was rejected. Replace
   those constraints with filtered unique indexes. */
DECLARE @drop NVARCHAR(MAX) = N'';

SELECT @drop = @drop + N'ALTER TABLE dbo.Payments DROP CONSTRAINT ' + QUOTENAME(kc.name) + N';'
FROM sys.key_constraints kc
JOIN sys.index_columns ic ON ic.object_id = kc.parent_object_id AND ic.index_id = kc.unique_index_id
JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE kc.parent_object_id = OBJECT_ID('dbo.Payments')
  AND kc.type = 'UQ'
  AND c.name IN ('GatewayOrderId', 'GatewayPaymentId', 'ReceiptNo', 'IdempotencyKey')
GROUP BY kc.name;

IF @drop <> N'' EXEC sp_executesql @drop;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Payments') AND name = 'UX_Payments_GatewayOrderId')
    CREATE UNIQUE INDEX UX_Payments_GatewayOrderId ON dbo.Payments (GatewayOrderId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Payments') AND name = 'UX_Payments_GatewayPaymentId')
    CREATE UNIQUE INDEX UX_Payments_GatewayPaymentId ON dbo.Payments (GatewayPaymentId) WHERE GatewayPaymentId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Payments') AND name = 'UX_Payments_ReceiptNo')
    CREATE UNIQUE INDEX UX_Payments_ReceiptNo ON dbo.Payments (ReceiptNo) WHERE ReceiptNo IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Payments') AND name = 'UX_Payments_IdempotencyKey')
    CREATE UNIQUE INDEX UX_Payments_IdempotencyKey ON dbo.Payments (IdempotencyKey) WHERE IdempotencyKey IS NOT NULL;
GO
