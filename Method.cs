using System;
using System.Collections.Generic;
using System.Text;
using MySql.Data.MySqlClient;
using Renci.SshNet;
using Renci.SshNet.Common;
using System.IO;
using static OTRS_Demon.Model;


namespace OTRS_Demon
{
    class Method
    {
        //обработчик события ввода пароля при подключении к серверу OTRS / имитация интерактивного ввода пароля
        public static void HandleKeyEvent(object sender, AuthenticationPromptEventArgs e)
        {
            foreach (AuthenticationPrompt prompt in e.Prompts)
            {
                if (prompt.Request.ToLower().IndexOf("password") != -1)
                {
                    prompt.Response = "hidden";
                }
            }
        }

        //очистка кэша OTRS
        public static void ClearOtrsCache(string login, string host, string commandText, string logPath)
        {
            KeyboardInteractiveAuthenticationMethod keybAuth = new KeyboardInteractiveAuthenticationMethod(login);
            keybAuth.AuthenticationPrompt += new EventHandler<AuthenticationPromptEventArgs>(HandleKeyEvent);
            ConnectionInfo connectionInfo = new ConnectionInfo(host, 22, login, keybAuth);
            using (SshClient client = new SshClient(connectionInfo))
            {
                client.Connect();
                client.RunCommand(commandText);
                client.Disconnect();
            }
            string message = "Выполнена очистка кэша OTRS";
            Console.WriteLine(message);
            WriteToLog(logPath, message);
        }

        //формирование списка заявок
        public static List<Ticket> GetTickets(int ticketStateId, string connectionString)
        {
            List<Ticket> Tickets = new List<Ticket>();
            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                string queryString = @"select t.id,
                                              t.title,
                                              t.ticket_state_id,
                                              t.ticket_priority_id,
                                              t.create_time
                                        from otrs.ticket t
                                        where t.ticket_state_id = @ticketStateId;";
                MySqlCommand cmd = new MySqlCommand(queryString, connection);
                cmd.Parameters.AddWithValue("ticketStateId", ticketStateId);
                MySqlDataReader reader = cmd.ExecuteReader();
                if (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        Ticket row = new Ticket
                        {
                            Id = reader.GetInt64(0),
                            Title = reader.GetString(1),
                            StateId = reader.GetInt32(2),
                            PriorityId = reader.GetInt32(3),
                            CreateTime = reader.GetDateTime(4)
                        };
                        Tickets.Add(row);
                    }
                }
                reader.Close();
            }
            return Tickets;
        }

        //формирование списка ticket_id из полученных заявок
        public static List<long> GetIds(List<Ticket> Tickets)
        {
            List<long> ticketsIdList = new List<long>();

            if (Tickets != null)
            {
                foreach (Ticket ticket in Tickets)
                {
                    ticketsIdList.Add(ticket.Id);
                }
            }

            return ticketsIdList;
        }

        //установка приоритета заявки
        public static void SetPriority(Ticket ticket, int priorityId, string connectionString, string logPath)
        {
            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();
                string queryString = $@"UPDATE otrs.ticket t
                                            SET
                                                t.ticket_priority_id = @priorityId,
                                                t.change_time = current_timestamp,
                                                change_by = 1
                                            where  t.id = @ticketId";
                MySqlCommand cmd = new MySqlCommand(queryString, connection);
                cmd.Parameters.AddWithValue("ticketId", ticket.Id);
                cmd.Parameters.AddWithValue("priorityId", priorityId);
                try
                {
                    cmd.Transaction = connection.BeginTransaction(System.Data.IsolationLevel.Serializable);
                    cmd.ExecuteNonQuery();
                    cmd.Transaction.Commit();
                    cmd.Transaction.Dispose();
                }
                catch (Exception e)
                {
                    cmd.Transaction.Rollback();
                    cmd.Transaction.Dispose();
                    throw new Exception("Ошибка записи значения приоритета в базу данных для ticket_id = " + ticket.Id + Environment.NewLine + e.Message + e.StackTrace);
                }
                string message = "Установлен " + GetParamValue(Model.TicketPriority.VeryHight.ToString()) + " приоритет для заявки: ticket_id = " + ticket.Id + " Тема: " + ticket.Title;
                Console.WriteLine(message);
                WriteToLog(logPath, message);
            }
        }

        //запись в лог файл
        public static void WriteToLog(string path, string message)
        {
            if (!File.Exists(path))
            {
                File.Create(path).Dispose();
            }
            using (StreamWriter file = new StreamWriter(path, true, Encoding.UTF8))
            {
                file.WriteLine(message);
            }
        }

        //получаем объект TimeSpan времени жизни заявки с момента ее регистрации в системе
        public static TimeSpan GetTicketLifeTime(Ticket ticket)
        {
            TimeSpan interval = DateTime.Now - ticket.CreateTime;
            return interval;
        }

        //установка типа задачи
        public static bool SetTaskType(string field_value, Ticket ticket, string logPath, string connectionString, string operationType)
        {
            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                long count = 0;// счетчик количества записей в таблице
                connection.Open();

                //проверяем наличие записи в базе
                string queryString = $@"select count(*) count
                                        from otrs.dynamic_field_value d
                                        where d.object_id = @object_id and d.field_id = 4;";
                MySqlCommand cmd = new MySqlCommand(queryString, connection);
                cmd.Parameters.AddWithValue("object_id", ticket.Id);
                count = (long)cmd.ExecuteScalar();

                //если записи нет, то добавляем запись
                if (count == 0 && operationType == "INSERT")
                {
                    string queryString1 = $@"INSERT INTO otrs.dynamic_field_value
                                                set field_id   = 4,
                                                    object_id  = @object_id,
                                                    value_text = @value_text;";
                    MySqlCommand cmd1 = new MySqlCommand(queryString1, connection);
                    cmd1.Parameters.AddWithValue("object_id", ticket.Id);
                    cmd1.Parameters.AddWithValue("value_text", field_value);
                    try
                    {
                        cmd1.Transaction = connection.BeginTransaction(System.Data.IsolationLevel.Serializable);
                        cmd1.ExecuteNonQuery();
                        cmd1.Transaction.Commit();
                        cmd1.Transaction.Dispose();
                    }
                    catch (Exception e)
                    {
                        cmd1.Transaction.Rollback();
                        cmd1.Transaction.Dispose();
                        throw new Exception("Ошибка записи значения типа задачи в базу данных для ticket_id = " + ticket.Id + Environment.NewLine + e.Message + e.StackTrace);
                    }

                    //MySqlDataReader reader1 = cmd1.ExecuteReader();
                    //reader1.Close();

                    string message = "Установлен тип задачи " + GetParamValue(field_value) + " для заявки " + ticket.Id + " Тема: " + ticket.Title;
                    Console.WriteLine(message);
                    WriteToLog(logPath, message);
                    return true;
                }

                //если запись есть, то обновляем значение типа задачи
                else if (count == 1 && operationType == "UPDATE")
                {
                    string queryString2 = $@"UPDATE otrs.dynamic_field_value d
                                                set value_text = @value_text
                                                where d.object_id = @object_id
                                                  and d.field_id = 4;";
                    MySqlCommand cmd2 = new MySqlCommand(queryString2, connection);
                    cmd2.Parameters.AddWithValue("object_id", ticket.Id);
                    cmd2.Parameters.AddWithValue("value_text", field_value);
                    try
                    {
                        cmd2.Transaction = connection.BeginTransaction(System.Data.IsolationLevel.Serializable);
                        cmd2.ExecuteNonQuery();
                        cmd2.Transaction.Commit();
                        cmd2.Transaction.Dispose();
                    }
                    catch (Exception e)
                    {
                        cmd2.Transaction.Rollback();
                        cmd2.Transaction.Dispose();
                        throw new Exception("Ошибка обновления значения типа задачи в базе данных для ticket_id = " + ticket.Id + Environment.NewLine + e.Message + e.StackTrace);
                    }

                    //MySqlDataReader reader2 = cmd2.ExecuteReader();
                    //reader2.Close();

                    string message = "Изменен тип задачи на " + GetParamValue(field_value) + " для заявки " + ticket.Id + " Тема: " + ticket.Title;
                    Console.WriteLine(message);
                    WriteToLog(logPath, message);
                    return true;
                }

                //если записей больше одной, то записываем ошибку в лог
                else if (count > 1)
                {
                    string message = "В таблице otrs.dynamic_field_value обнаружены дублирующиеся строки с object_id = " + ticket.Id + " и field_id = " + TaskType.Id;
                    Console.WriteLine(message);
                    WriteToLog(logPath, message);
                }
            }
            return false;
        }

        //установка сложности
        public static bool SetСomplexity(string field_value, Ticket ticket, string logPath, string connectionString, string operationType)
        {
            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                long count = 0;//счетчик количества записей в таблице
                connection.Open();

                //проверяем наличие записи в базе
                string queryString = $@"select count(*) count
                                        from otrs.dynamic_field_value d
                                        where d.object_id = @object_id and d.field_id = 3;";
                MySqlCommand cmd = new MySqlCommand(queryString, connection);
                cmd.Parameters.AddWithValue("object_id", ticket.Id);
                count = (long)cmd.ExecuteScalar();

                //если записи нет, то добавляем запись
                if (count == 0 && operationType == "INSERT")
                {
                    string queryString1 = $@"INSERT INTO otrs.dynamic_field_value
                                                set field_id   = 3,
                                                    object_id  = @object_id,
                                                    value_text = @value_text;";
                    MySqlCommand cmd1 = new MySqlCommand(queryString1, connection);
                    cmd1.Parameters.AddWithValue("object_id", ticket.Id);
                    cmd1.Parameters.AddWithValue("value_text", field_value);
                    try
                    {
                        cmd1.Transaction = connection.BeginTransaction(System.Data.IsolationLevel.Serializable);
                        cmd1.ExecuteNonQuery();
                        cmd1.Transaction.Commit();
                        cmd1.Transaction.Dispose();
                    }
                    catch (Exception e)
                    {
                        cmd1.Transaction.Rollback();
                        cmd1.Transaction.Dispose();
                        throw new Exception("Ошибка записи значения сложности в базу данных для ticket_id = " + ticket.Id + Environment.NewLine + e.Message + e.StackTrace);
                    }

                    //MySqlDataReader reader1 = query1.ExecuteReader();
                    //reader1.Close();

                    string message = "Установлена " + GetParamValue(field_value) + " сложность для заявки " + ticket.Id + " Тема: " + ticket.Title;
                    Console.WriteLine(message);
                    WriteToLog(logPath, message);
                    return true;
                }

                //если запись есть, то обновляем значение сложности
                else if (count == 1 && operationType == "UPDATE")
                {
                    string queryString2 = $@"UPDATE otrs.dynamic_field_value d
                                                set value_text = @value_text
                                                where d.object_id = @object_id
                                                  and d.field_id = 3;";
                    MySqlCommand cmd2 = new MySqlCommand(queryString2, connection);
                    cmd2.Parameters.AddWithValue("object_id", ticket.Id);
                    cmd2.Parameters.AddWithValue("value_text", field_value);
                    try
                    {
                        cmd2.Transaction = connection.BeginTransaction(System.Data.IsolationLevel.Serializable);
                        cmd2.ExecuteNonQuery();
                        cmd2.Transaction.Commit();
                        cmd2.Transaction.Dispose();
                    }
                    catch (Exception e)
                    {
                        cmd2.Transaction.Rollback();
                        cmd2.Transaction.Dispose();
                        throw new Exception("Ошибка обновления значения сложности в базе данных для ticket_id = " + ticket.Id + Environment.NewLine + e.Message + e.StackTrace);
                    }

                    //MySqlDataReader reader2 = cmd2.ExecuteReader();
                    //reader2.Close();

                    string message = "Изменена сложность на " + GetParamValue(field_value) + " для заявки " + ticket.Id + " Тема: " + ticket.Title;
                    Console.WriteLine(message);
                    WriteToLog(logPath, message);
                    return true;
                }

                //если записей больше одной, то записываем ошибку в лог
                else if (count > 1)
                {
                    string message = "В таблице otrs.dynamic_field_value обнаружены дублирующиеся строки с object_id = " + ticket.Id + " и field_id = " + Сomplexity.Id;
                    Console.WriteLine(message);
                    WriteToLog(logPath, message);
                }
            }
            return false;
        }

        //преобразование значений параметров в читаемый текст
        public static string GetParamValue(string param)
        {
            string value = string.Empty;

            switch (param)
            {
                case "diff1":
                    value = "низкая";
                    break;

                case "diff2":
                    value = "средняя";
                    break;

                case "diff3":
                    value = "высокая";
                    break;

                case "z1":
                    value = "Система Заказ";
                    break;

                case "z2":
                    value = "Справка";
                    break;

                case "z3":
                    value = "Маркетолог";
                    break;

                case "z4":
                    value = "Администрирование";
                    break;

                case "1":
                    value = "очень низкий";
                    break;

                case "2":
                    value = "низкий";
                    break;

                case "3":
                    value = "средний";
                    break;

                case "4":
                    value = "высокий";
                    break;

                case "5":
                    value = "наивысший";
                    break;

                default:
                    value = "значение параметра не определено";
                    break;
            }

            return value;
        }
    }
}
