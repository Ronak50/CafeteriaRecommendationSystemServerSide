using CafeteriaRecommendationSystem.Models;
using CafeteriaRecommendationSystem.Services;
using MySqlX.XDevAPI;
using System;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
namespace CafeteriaRecommendationSystem.ClientHandler
{ 
    internal class ClientHandler
    {
        public static void HandleClient(object obj)
        {

            TcpClient client = (TcpClient)obj;
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[4096];
            int byteCount;

            LoginService sessionManager = new LoginService();
            UserSessionInfo currentSessionInfo = null;
            string currentEmail = string.Empty;
            string response;

            try
            {
                while ((byteCount = stream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    string request = Encoding.ASCII.GetString(buffer, 0, byteCount);
                    Console.WriteLine("Received: " + request);
                    string[] requestParts = request.Trim().Split(':');
                    string email = requestParts[0];
                    string action = requestParts.Length > 1 ? requestParts[1] : "";
                    string parameters = requestParts.Length > 2 ? requestParts[2] : "";

                    LoginResult result = Server.LoginUser(email);

                    if (result.Success && action == "")
                    {
                        response = $"Login successful as {result.Role} with UserId {result.UserId}.";

                        currentSessionInfo = sessionManager.LogUserLogin(result.UserId);
                        DateTime currentLoginTime = currentSessionInfo.LoginTime;
                        currentEmail = email;

                        DateTime? lastLoginTime = sessionManager.GetLastLoginTime(result.UserId, currentLoginTime);
                        StringBuilder notificationResponse = new StringBuilder();

                        if (result.Role == "Employee")
                        { 
                            var notifications = sessionManager.GetUnreadNotifications(lastLoginTime);

                            if (notifications.Count != 0)
                            {
                                notificationResponse.AppendLine("\nNotifications:");
                            }

                            for (int i = 0; i < notifications.Count; i++)
                            {
                                notificationResponse.AppendLine($"{i + 1}. {notifications[i]}");
                            }
                        }

                        response += notificationResponse.ToString();
                        byte[] responseData = Encoding.ASCII.GetBytes(response);
                        stream.Write(responseData, 0, responseData.Length);

                    }

                    else if (action.ToLower() == "logout")
                    {
                        response = Server.LogoutUser(email);
                        byte[] responseData = Encoding.ASCII.GetBytes(response);
                        stream.Write(responseData, 0, responseData.Length);

                        if (response.Contains("Logout successful"))
                        {
                            sessionManager.LogUserLogout(currentSessionInfo.SessionID);
                        }

                        stream.Close();
                        client.Close();
                        Console.WriteLine("Client logged out and disconnected.");
                        return;
                    }
                    else
                    {

                        if (result.Success && action != "")
                        {
                            response = Server.ExecuteRoleBasedFunctionality(result.Role, action, parameters);
                        }
                        else if (result.Success)
                        {
                            response = $"Login successful as {result.Role} with UserId {result.UserId}. ";
                        }
                        else
                        {
                            response = "Login failed";
                        }

                        byte[] responseData = Encoding.ASCII.GetBytes(response);
                        stream.Write(responseData, 0, responseData.Length);
                    }
                }
            }
            catch (Exception e)
            {
                HandleClientDisconnection(sessionManager, currentSessionInfo, currentEmail);
                Console.WriteLine("Client Disconnected");
            }
        }
            public static void HandleClientDisconnection(LoginService sessionManager, UserSessionInfo currentSessionInfo, string currentEmail)
            {
                if (currentSessionInfo.SessionID > 0 && !string.IsNullOrEmpty(currentEmail))
                {
                    sessionManager.LogUserLogout(currentSessionInfo.SessionID);
                    Server.LogoutUser(currentEmail);
                    Console.WriteLine("Client logged out and session ended.");
                }
            }
        }
        }
    
