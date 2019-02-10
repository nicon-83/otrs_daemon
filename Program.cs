using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.IO;
using static OTRS_Demon.Method;
using static OTRS_Demon.Model;

namespace OTRS_Demon
{
    class Program
    {
        private static readonly string connectionString = new MySqlConnectionStringBuilder
        {
            Server = "hidden",
            UserID = "hidden",
            Password = "hidden",
            Database = "hidden",
            Pooling = true,
        }.ConnectionString;

        static void Main(string[] args)
        {
            try
            {
                string logPath = Path.Combine(Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory), "otrs.log");
                List<Ticket> newTickets = GetTickets(TicketState.New, connectionString);
                List<Ticket> openTickets = GetTickets(TicketState.Open, connectionString);
                WriteToLog(logPath, new string('-', 80) + Environment.NewLine + DateTime.Now + " Запуск скрипта");

                //обработка новых заявок
                int newCounter = 0;
                if (newTickets.Count != 0)
                {
                    foreach (Ticket ticket in newTickets)
                    {
                        bool result = SetСomplexity(Сomplexity.Low, ticket, logPath, connectionString, OperationType.Insert);
                        if (result) newCounter++;

                        bool result1 = SetTaskType(TaskType.SystemOrder, ticket, logPath, connectionString, OperationType.Insert);
                        if (result1) newCounter++;

                        if ((ticket.Title.ToLower().Contains("срочно") || ticket.Title.Contains("!!!Сообщение")) && ticket.PriorityId != TicketPriority.VeryHight)
                        {
                            SetPriority(ticket, TicketPriority.VeryHight, connectionString, logPath);
                            newCounter++;
                        }
                    }
                    if (newCounter == 0)
                    {
                        string message = "Новые заявки есть, но внесение изменений не требуется";
                        Console.WriteLine(message);
                        WriteToLog(logPath, message);
                    }
                }
                else
                {
                    string message = "Новых заявок нет";
                    Console.WriteLine(message);
                    WriteToLog(logPath, message);
                }

                //обработка открытых заявок
                int openCounter = 0;
                if (openTickets.Count != 0)
                {
                    foreach (Ticket ticket in openTickets)
                    {
                        if (GetTicketLifeTime(ticket).TotalMinutes < 2)
                        {
                            bool result = false;
                            if (ticket.PriorityId != TicketPriority.Hight)
                            {
                                result = SetСomplexity(Сomplexity.High, ticket, logPath, connectionString, OperationType.Update);
                            }
                            if (result) openCounter++;
                        }
                    }
                    if (openCounter == 0)
                    {
                        string message = "Заявки в работе есть, но внесение изменений не требуется";
                        Console.WriteLine(message);
                        WriteToLog(logPath, message);
                    }
                }
                else
                {
                    string message = "Заявок в работе нет";
                    Console.WriteLine(message);
                    WriteToLog(logPath, message);
                }

                if (newCounter > 0 || openCounter > 0)
                {
                    ClearOtrsCache("otrs", "192.168.0.226", "otrs.Console.pl Maint::Cache::Delete", logPath);
                }

                WriteToLog(logPath, DateTime.Now + " Завершение работы скрипта");
            }
            catch (Exception e)
            {
                string logPath = Path.Combine(Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory), "otrs.log");
                Console.WriteLine("Ошибка: " + e.Message + Environment.NewLine + e.StackTrace);
                WriteToLog(logPath, "Ошибка: " + e.Message + Environment.NewLine + e.StackTrace);
            }
        }
    }
}
