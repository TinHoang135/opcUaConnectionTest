namespace PG.LIFT.Integrations.EMMS.Services
{
    public static class ServiceConstants
    {
        // ZMQ topics
        public const string zmq_Topic_AQL_Roll_A_Active = "AQL_Roll_A_Active";
        public const string zmq_Topic_AQL_Roll_A_Diameter = "AQL_Roll_A_Diameter";
        public const string zmq_Topic_AQL_Roll_B_Diameter = "AQL_Roll_B_Diameter";
        public const string zmq_Topic_CC_Roll_A_Active = "CC_Roll_A_Active";
        public const string zmq_Topic_CC_Roll_A_Diameter = "CC_Roll_A_Diameter";
        public const string zmq_Topic_CC_Roll_B_Diameter = "CC_Roll_B_Diameter";
        public const string zmq_Topic_DS_Roll_A_Active = "DS_Roll_A_Active";
        public const string zmq_Topic_DS_Roll_A_Diameter = "DS_Roll_A_Diameter";
        public const string zmq_Topic_DS_Roll_B_Diameter = "DS_Roll_B_Diameter";
        public const string zmq_Topic_TS_Roll_A_Active = "TS_Roll_A_Active";
        public const string zmq_Topic_TS_Roll_A_Diameter = "TS_Roll_A_Diameter";
        public const string zmq_Topic_TS_Roll_B_Diameter = "TS_Roll_B_Diameter";
        public const string zmq_Topic_Cuff_Roll_A_Active = "Cuff_Roll_A_Active";
        public const string zmq_Topic_Cuff_Roll_A_Diameter = "Cuff_Roll_A_Diameter";
        public const string zmq_Topic_Cuff_Roll_B_Diameter = "Cuff_Roll_B_Diameter";
        public const string zmq_Topic_IBU_Roll_A_Active = "IBU_Roll_A_Active";
        public const string zmq_Topic_IBU_Roll_A_Diameter = "IBU_Roll_A_Diameter";
        public const string zmq_Topic_IBU_Roll_B_Diameter = "IBU_Roll_B_Diameter";
        public const string zmq_Topic_OBU_Roll_A_Active = "OBU_Roll_A_Active";
        public const string zmq_Topic_OBU_Roll_A_Diameter = "OBU_Roll_A_Diameter";
        public const string zmq_Topic_OBU_Roll_B_Diameter = "OBU_Roll_B_Diameter";
        public const string zmq_Topic_Line_State = "f_Main.C_DA_LineState";
        public const string zmq_Topic_Espresso_Auto_Mode = "Espresso_Auto_Mode";

        // ZMQ endpoint
        public const string ZmqPublisherEU34 = "tcp://143.28.52.174:5566";
        public const string ZmqPublisherMP48 = "tcp://151.208.190.74:5566";
    }
}
