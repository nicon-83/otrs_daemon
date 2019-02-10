using System;

namespace OTRS_Demon
{
    sealed class Model
    {
        public class Ticket
        {
            public long Id { get; set; }
            public string Title { get; set; }
            public int StateId { get; set; }
            public int PriorityId { get; set; }
            public DateTime CreateTime { get; set; }
        }

        public class TicketState
        {
            public readonly static int New = 1;
            public readonly static int ClosedSuccessful = 2;
            public readonly static int ClosedUnsuccessful = 3;
            public readonly static int Open = 4;
            public readonly static int Removed = 5;
            public readonly static int PendingReminder = 6;
            public readonly static int PendingAutoClosePlus = 7;
            public readonly static int PendingAutoCloseMinus = 8;
            public readonly static int Merged = 9;
            public readonly static int CloseWithoutNotice = 10;
            public readonly static int OpenedAfterAlreadyClosed = 11;
            public readonly static int BlockedAfterReturning = 12;
        }

        public class TicketPriority
        {
            public readonly static int VeryLow = 1;
            public readonly static int Low = 2;
            public readonly static int Normal = 3;
            public readonly static int Hight = 4;
            public readonly static int VeryHight = 5;
        }

        public class Сomplexity
        {
            public readonly static int Id = 3;
            public readonly static string Low = "diff1";
            public readonly static string Normal = "diff2";
            public readonly static string High = "diff3";
        }

        public class TaskType
        {
            public readonly static int Id = 4;
            public readonly static string SystemOrder = "z1"; //Система заказ
            public readonly static string Help = "z2"; //Справка
            public readonly static string Marketolog = "z3"; //Маркетолог
            public readonly static string Administration = "z4"; //Администрирование
        }

        public class OperationType
        {
            public readonly static string Update = "UPDATE";
            public readonly static string Insert = "INSERT";
        }
    }
}
