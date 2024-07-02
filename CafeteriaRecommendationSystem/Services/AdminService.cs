﻿using CafeteriaRecommendationSystem.Models;
using MySql.Data.MySqlClient;
using System;
using System.Text;
using System.IO;

namespace CafeteriaRecommendationSystem.Services
{
    internal class AdminService
    {
        public static string AdminFunctionality(string action, string parameters)
        {
            switch (action)
            {
                case "additem":
                    return AddMenuItem(parameters);
                case "updateitem":
                    return UpdateMenuItem(parameters);
                case "deleteitem":
                    return DeleteMenuItem(parameters);
                case "viewitems":
                    return ViewMenuItems();
                case "dicardmenuitems":
                    return DiscardMenuItemList();
                default:
                    return "Please enter a valid option.";
            }
        }

        public static string DiscardMenuItemList()
        {
            try
            {
                using (MySqlConnection connection = DatabaseUtility.GetConnection())
                {
                    connection.Open();
                    var negativeWords = File.ReadAllLines(@"C:\Users\ronak.sharma\source\repos\CafeteriaRecommendationSystem\CafeteriaRecommendationSystem\Data\negative_words.txt");

                    var likeClauses = new StringBuilder();
                    foreach (var word in negativeWords)
                    {
                        if (likeClauses.Length > 0)
                        {
                            likeClauses.Append(" OR ");
                        }
                        likeClauses.Append($"s.CommentSentiments LIKE '%{word}%'");
                    }

                    string query = "SELECT i.ItemId, i.Name, s.OverallRating, s.CommentSentiments FROM Item i INNER JOIN Sentiment s ON i.ItemId = s.ItemId WHERE s.OverallRating < 2 AND (" + likeClauses.ToString() + ")";


                    using (MySqlCommand cmd = new MySqlCommand(query, connection))
                    {
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (!reader.HasRows)
                            {
                                return "Discard Menu Item List";
                            }

                            var result = new StringBuilder();
                            result.AppendLine("\nItems to be discarded:");
                            result.AppendLine("------------------------------------------------------------------------------");
                            result.AppendLine($"{"ItemId",-10} {"Name",-25} {"OverallRating",-15} {"CommentSentiments"}");
                            result.AppendLine("------------------------------------------------------------------------------");

                            while (reader.Read())
                            {
                                result.AppendLine(
                                $"{reader.GetInt32("ItemId"),-10} " +
                                $"{reader.GetString("Name"),-25} " +
                                $"{reader.GetFloat("OverallRating"),-15} " +
                                $"{reader.GetString("CommentSentiments")}");
                            }
                            return result.ToString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return "Error retrieving discard menu items: " + ex.Message;
            }
        }


        public static string AddMenuItem(string parameters)
        {
            string[] paramParts = parameters.Split(';');
            if (paramParts.Length < 4)
            {
                return "Admin: Invalid parameters for adding item";
            }

            string name = paramParts[0];
            decimal price;
            bool availabilityStatus;
            string mealType = paramParts[3];

            if (!decimal.TryParse(paramParts[1], out price) || !bool.TryParse(paramParts[2], out availabilityStatus))
            {
                return "Admin: Invalid parameters for adding item";
            }

            try
            {
                using (MySqlConnection connection = DatabaseUtility.GetConnection())
                {
                    connection.Open();
                    int mealTypeId;
                    string getMealTypeIdQuery = "SELECT meal_type_id FROM MealType WHERE type = @Type";
                    using (MySqlCommand getMealTypeIdCmd = new MySqlCommand(getMealTypeIdQuery, connection))
                    {
                        getMealTypeIdCmd.Parameters.AddWithValue("@Type", mealType);
                        object result = getMealTypeIdCmd.ExecuteScalar();
                        if (result == null)
                        {
                            return "Admin: Invalid meal type";
                        }
                        mealTypeId = Convert.ToInt32(result);
                    }

                    int itemId;
                    string query = "INSERT INTO Item (Name, Price, AvailabilityStatus, MealTypeId) VALUES (@Name, @Price, @AvailabilityStatus,@MealTypeId)";
                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@Name", name);
                        command.Parameters.AddWithValue("@Price", price);
                        command.Parameters.AddWithValue("@AvailabilityStatus", availabilityStatus);
                        command.Parameters.AddWithValue("@MealTypeId", mealTypeId);
                        command.ExecuteNonQuery();
                        itemId = (int)command.LastInsertedId;
                    }

                    string notificationMessage = $"Item '{name}' added to the menu.";
                    string insertNotificationQuery = "INSERT INTO Notification (Message, NotificationDate) VALUES (@Message, @NotificationDate)";
                    using (MySqlCommand insertNotificationCmd = new MySqlCommand(insertNotificationQuery, connection))
                    {
                        insertNotificationCmd.Parameters.AddWithValue("@Message", notificationMessage);
                        insertNotificationCmd.Parameters.AddWithValue("@NotificationDate", DateTime.Now);
                        insertNotificationCmd.ExecuteNonQuery();
                    }
                    return "Admin: Item added successfully";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Database exception: " + ex.Message);
                return "Admin: Failed to add item";
            }
        }

        public static string UpdateMenuItem(string parameters)
        {
            string[] paramParts = parameters.Split(';');
            if (paramParts.Length < 5)
            {
                return "Admin: Invalid parameters for updating item";
            }

            int itemId;
            string name = paramParts[1];
            decimal price;
            bool availabilityStatus;
            string mealType = paramParts[4];

            if (!int.TryParse(paramParts[0], out itemId) || !decimal.TryParse(paramParts[2], out price) || !bool.TryParse(paramParts[3], out availabilityStatus))
            {
                return "Admin: Invalid parameters for updating item";
            }

            try
            {
                using (MySqlConnection connection = DatabaseUtility.GetConnection())
                {
                    connection.Open();
                    int mealTypeId;
                    string getMealTypeIdQuery = "SELECT meal_type_id FROM MealType WHERE type = @Type";
                    using (MySqlCommand getMealTypeIdCmd = new MySqlCommand(getMealTypeIdQuery, connection))
                    {
                        getMealTypeIdCmd.Parameters.AddWithValue("@Type", mealType);
                        object result = getMealTypeIdCmd.ExecuteScalar();
                        if (result == null)
                        {
                            return "Admin: Invalid meal type";
                        }
                        mealTypeId = Convert.ToInt32(result);
                    }

                    string query = "UPDATE Item SET Name = @Name, Price = @Price, AvailabilityStatus = @AvailabilityStatus, MealTypeId = @MealTypeId WHERE ItemId = @ItemId";
                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@ItemId", itemId);
                        command.Parameters.AddWithValue("@Name", name);
                        command.Parameters.AddWithValue("@Price", price);
                        command.Parameters.AddWithValue("@AvailabilityStatus", availabilityStatus);
                        command.Parameters.AddWithValue("@MealTypeId", mealTypeId);
                        command.ExecuteNonQuery();
                        return "Admin: Item updated successfully";
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Database exception: " + ex.Message);
                return "Admin: Failed to update item";
            }
        }

        public static string DeleteMenuItem(string parameters)
        {
            int itemId;

            if (!int.TryParse(parameters, out itemId))
            {
                return "Admin: Invalid item ID for deletion";
            }
            try
            {
                using (MySqlConnection connection = DatabaseUtility.GetConnection())
                {
                    connection.Open();
                    string checkQuery = "SELECT COUNT(*) FROM Item WHERE ItemId = @ItemId";
                    using (MySqlCommand checkCmd = new MySqlCommand(checkQuery, connection))
                    {
                        checkCmd.Parameters.AddWithValue("@ItemId", itemId);
                        long count = (long)checkCmd.ExecuteScalar();

                        if (count == 0)
                        {
                            return "Admin: Item ID not found";
                        }
                    }
                    string deleteQuery = "DELETE FROM Item WHERE ItemId = @ItemId";
                    using (MySqlCommand deleteCmd = new MySqlCommand(deleteQuery, connection))
                    {
                        deleteCmd.Parameters.AddWithValue("@ItemId", itemId);
                        deleteCmd.ExecuteNonQuery();
                        return "Admin: Item deleted successfully";
                    }
                }
            }

            catch (Exception ex)
            {
                Console.WriteLine("Database exception: " + ex.Message);
                return "Admin: Failed to delete item";
            }
        }

        public static string ViewMenuItems()
        {
            try
            {
                using (MySqlConnection connection = DatabaseUtility.GetConnection())
                {
                    connection.Open();
                    string query = "SELECT i.ItemId, i.Name, i.Price, i.AvailabilityStatus, m.type AS MealType " +
                               "FROM Item i " +
                               "INNER JOIN MealType m ON i.MealTypeId = m.meal_type_id " +
                               "ORDER BY i.ItemId";

                    using (MySqlCommand cmd = new MySqlCommand(query, connection))
                    {
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                StringBuilder result = new StringBuilder();
                                result.AppendLine("\nItems List:");
                                result.AppendLine("--------------------------------------------------------------------");
                                result.AppendLine($"{"ID",-5} {"Name",-20} {"Price",-12} {"Availability",-15} {"Meal Type",-12}");
                                result.AppendLine("--------------------------------------------------------------------");

                                while (reader.Read())
                                {
                                    result.AppendLine($"{reader.GetInt32("ItemId"),-5} " +
                                                       $"{reader.GetString("Name"),-20} " +
                                                       $"Rs. {reader.GetDecimal("Price"),-12:f2} " +
                                                       $"{(reader.GetBoolean("AvailabilityStatus") ? "True" : "False"),-15} " +
                                                       $"{(reader.IsDBNull(reader.GetOrdinal("MealType")) ? "N/A" : reader.GetString("MealType")),-12}");
                                }
                                result.AppendLine("--------------------------------------------------------------------\n");
                                result.AppendLine("--------------------------------------------------------------------");
                                return result.ToString();
                            }
                            else
                            {
                                return "Admin: No items found";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Database exception: " + ex.Message);
                return "Admin: Failed to retrieve items";
            }
        }

    }
}
