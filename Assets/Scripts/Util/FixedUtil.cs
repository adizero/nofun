namespace Nofun.Util
{
    public static class FixedUtil
    {
        public static float Fixed9PointToFloat(short num)
        {
            return num / 1024.0f;
        }

        public static float FixedToFloat(int num)
        {
            return num / 16384.0f;
        }
    }
}