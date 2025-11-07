using System.Windows;

namespace namm
{
    public static class UIHelper
    {
        /// <summary>
        /// Tính toán và trả về một Thickness đã được giới hạn để đảm bảo icon không tràn ra ngoài.
        /// </summary>
        /// <param name="left">Lề trái mong muốn.</param>
        /// <param name="top">Lề trên mong muốn.</param>
        /// <param name="right">Lề phải mong muốn.</param>
        /// <param name="bottom">Lề dưới mong muốn.</param>
        /// <returns>Một đối tượng Thickness đã được giới hạn.</returns>
        public static Thickness GetConstrainedMargin(double left, double top, double right, double bottom)
        {
            // Giới hạn: Đảm bảo tổng lề ngang không quá nhỏ để icon không bị tràn.
            // Ví dụ: tổng lề ngang tối thiểu là 40px.
            if (left + right < 40)
            {
                // Nếu tổng lề quá nhỏ, đặt mỗi bên là 20 để đảm bảo cân đối và không tràn.
                left = 20;
                right = 20;
            }

            return new Thickness(left, top, right, bottom);
        }
    }
}