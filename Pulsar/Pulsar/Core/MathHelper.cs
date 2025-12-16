namespace Pulsar.Core
{
    public static class MathHelper
    {
        public const double DefaultTriggerDistance = 60.0;

        /// <summary>
        /// 计算极坐标槽位 (0-8)
        /// </summary>
        public static int CalculateRadialSlot(double dx, double dy, double triggerDistance = DefaultTriggerDistance)
        {
            // 1. 死区判定
            double distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance < triggerDistance) return 0;

            // 2. 角度计算 (-PI ~ PI)
            // WPF 坐标系中 (0, -1) 为上方，Atan2(-1, 0) = -PI/2
            double angle = Math.Atan2(dy, dx);

            // 3. 旋转坐标系，使正上方对应 0 度
            double theta = angle + Math.PI / 2.0;

            // 规范化到 0 ~ 2PI
            if (theta < 0)
            {
                theta += 2 * Math.PI;
            }

            // 4. 扇区划分 (偏移半个扇区 22.5度 以实现居中对齐)
            double sectorAngle = theta + (Math.PI / 8.0);
            int sectorIdx = (int)Math.Floor(sectorAngle / (Math.PI / 4.0));

            // 5. 映射到 1-8
            int slot = (sectorIdx % 8) + 1;
            return slot;
        }

        public static double RadiansToDegrees(double radians)
        {
            return radians * (180.0 / Math.PI);
        }
    }
}