-- ====================================================================
-- Script cập nhật thông tin tài khoản 'admin'
-- Đổi DisplayName thành 'nam' và mật khẩu thành '123456'
-- ====================================================================

-- Tùy chỉnh thông tin tài khoản Admin tại đây
DECLARE @AdminUserName NVARCHAR(100) = 'admin';
DECLARE @NewDisplayName NVARCHAR(100) = 'nam';
DECLARE @NewPassword NVARCHAR(1000) = '123456'; -- !!! CẢNH BÁO: Mật khẩu nên được băm (hashed) thay vì lưu dạng văn bản thuần.

-- Kiểm tra xem tài khoản 'admin' đã tồn tại chưa
IF EXISTS (SELECT 1 FROM dbo.Account WHERE UserName = @AdminUserName)
BEGIN
    -- Nếu tồn tại, cập nhật thông tin
    UPDATE dbo.Account
    SET 
        DisplayName = @NewDisplayName,
        Password = @NewPassword
    WHERE 
        UserName = @AdminUserName;

    PRINT 'Tài khoản Admin [' + @AdminUserName + '] đã được cập nhật thành công.';
END
ELSE
BEGIN
    -- Nếu chưa tồn tại, in thông báo
    PRINT 'Không tìm thấy tài khoản Admin [' + @AdminUserName + '] để cập nhật.';
END
GO
