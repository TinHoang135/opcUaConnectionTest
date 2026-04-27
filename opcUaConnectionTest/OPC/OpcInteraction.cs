using Opc.Ua;

namespace opcUaConnectionTest.OPC
{
    public sealed class OpcInteraction
    {
        public static short? TryConvertToInt16(DataValue? dataValue)
        {
            if (!HasValidValue(dataValue))
                return null;

            try
            {
                return Convert.ToInt16(dataValue!.Value);
            }
            catch (Exception) when (
                dataValue is not null &&
                (dataValue.Value is IConvertible || dataValue.Value is string))
            {
                return null;
            }
        }

        public static int? TryConvertToInt32(DataValue? dataValue)
        {
            if (!HasValidValue(dataValue))
                return null;

            try
            {
                return Convert.ToInt32(dataValue!.Value);
            }
            catch (Exception) when (
                dataValue is not null &&
                (dataValue.Value is IConvertible || dataValue.Value is string))
            {
                return null;
            }
        }

        private static bool HasValidValue(DataValue? dataValue)
        {
            if (dataValue == null)
                return false;

            if (StatusCode.IsBad(dataValue.StatusCode))
                return false;

            return dataValue.Value != null;
        }

    }
}