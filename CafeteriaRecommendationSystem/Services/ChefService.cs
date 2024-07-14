using MySql.Data.MySqlClient;
using System;
using System.Text;
using System.IO;

namespace CafeteriaRecommendationSystem.Services
{
    internal class ChefService
    {
        public static string ChefFunctionality(string action, string parameters)
        {
            switch (action.ToLower())
            {
                case "recommenditem":
                    string[] paramParts = parameters.Split(';');
                    if (paramParts.Length == 2)
                    {
                        string menuType = paramParts[0];
                        if (int.TryParse(paramParts[1], out int size))
                        {
                            return RecEngineGetFoodItemForNextDay(menuType, size);
                        }
                        else
                        {
                            return "Invalid size parameter.";
                        }
                    }
                    else
                    {
                        return "Invalid parameters for recommenditem.";
                    }
                case "viewfeedback":
                    return ViewFeedback();
                case "viewemployeevote":
                    return ViewEmployeeVote();
                case "viewmenuitem":
                    return ViewMenuItems();
                case "rolloutmenu":
                    if (int.TryParse(parameters, out int itemId))
                    {
                        return InsertChefRecommendation(itemId);
                    }
                    else
                    {
                        return "Invalid Item ID.";
                    }
                case "discardmenuitems":
                    return DiscardMenuItemList();
                case "removefooditem":
                    return RemoveFoodItem(parameters);
                case "insertfeedbacknotification":
                    return InsertFeedbackNotification(parameters);
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
                                return "No items to discard.";
                            }

                            var result = new StringBuilder();
                            result.AppendLine("\nDiscard Menu Item List");
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

                            result.AppendLine();
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

        public static string GetItemNameById(string itemId)
        {
                using (MySqlConnection connection = DatabaseUtility.GetConnection())
                {
                    connection.Open();
                    string selectQuery = "SELECT Name FROM Item WHERE ItemId = @ItemId";
                    using (MySqlCommand cmd = new MySqlCommand(selectQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@ItemId", itemId);
                        object result = cmd.ExecuteScalar();
                        if (result != null)
                        {
                            return result.ToString();
                        }
                        else
                        {
                            return null;
                        }
                    }
                }
        }

        public static string RemoveFoodItem(string itemId)
        {
            DateTime today = DateTime.Now;
            if (today.Day != 1)
            {
                return "Food items can only be removed on the first day of the month.";
            }
            try
            {
                string itemName = GetItemNameById(itemId);
                if (itemName == null)
                {
                    return $"Item with ID '{itemId}' not found.";
                }
                using (MySqlConnection connection = DatabaseUtility.GetConnection())
                {
                    connection.Open();
                    string deleteQuery = "DELETE FROM Item WHERE Name = @ItemName";
                    using (MySqlCommand cmd = new MySqlCommand(deleteQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@ItemName", itemId);
                        int rowsAffected = cmd.ExecuteNonQuery();
                        if (rowsAffected > 0)
                        {
                            return $"Successfully removed item: {itemName}";
                        }
                        else
                        {
                            return $"Item '{itemName}' not found.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return "Error removing item: " + ex.Message;
            }
        }
        public static string InsertFeedbackNotification(string itemId)
        {
            try
            {
                string itemName = GetItemNameById(itemId);
                if (itemName == null)
                {
                    return $"Item with ID '{itemId}' not found.";
                }
                using (MySqlConnection connection = DatabaseUtility.GetConnection())
                {
                    connection.Open();

                    string insertQuery = "INSERT INTO Notification (Message, NotificationDate) VALUES (@Message, @NotificationDate)";
                    using (MySqlCommand cmd = new MySqlCommand(insertQuery, connection))
                    {
                        string notificationMessage = $"We are trying to improve your experience with {itemName}. Please provide your feedback and help us";
                        cmd.Parameters.AddWithValue("@Message", notificationMessage);
                        cmd.Parameters.AddWithValue("@NotificationDate", DateTime.Now);
                        cmd.ExecuteNonQuery();
                    }
                }
                return "Feedback notification inserted successfully.";
            }
            catch (Exception ex)
            {
                return "Error inserting feedback into notification table: " + ex.Message;
            }
        }
        public static string ViewEmployeeVote()
        {
            try
            {
                using (MySqlConnection connection = DatabaseUtility.GetConnection())
                {
                    connection.Open();
                    string query = "SELECT EmployeeVoteId, ItemId, VoteTime, VoteCount FROM EmployeeVote";

                    using (MySqlCommand cmd = new MySqlCommand(query, connection))
                    {
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                var result = new StringBuilder();
                                result.AppendLine("\nEmployee Votes:");
                                result.AppendLine("-------------------------------------------------------------");
                                result.AppendLine($"{"EmployeeVoteId",-18} {"ItemId",-8} {"VoteTime",-25} {"VoteCount",-10}");
                                result.AppendLine("-------------------------------------------------------------");

                                while (reader.Read())
                                {
                                    result.AppendLine(
                                        $"{reader.GetInt32("EmployeeVoteId"),-18} " +
                                        $"{reader.GetInt32("ItemId"),-8} " +
                                        $"{reader.GetDateTime("VoteTime"),-25} " +
                                        $"{reader.GetInt32("VoteCount"),-10}");
                                }

                                return result.ToString();
                            }
                            else
                            {
                                return "No employee votes found.";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Database exception: " + ex.Message);
                return "Failed to retrieve employee votes.";
            }
        }


        public static string InsertChefRecommendation(int itemId)
        {
            try
            {
                int sentimentId = GetSentimentId(itemId);

                if (sentimentId == -1)
                {
                    return "No sentiment comments are available for this particular ItemID";
                }

                using (MySqlConnection connection = DatabaseUtility.GetConnection())
                {
                    connection.Open();
                    string checkQuery = "SELECT COUNT(*) FROM Recommendation WHERE ItemId = @ItemId";
                    using (MySqlCommand checkCommand = new MySqlCommand(checkQuery, connection))
                    {
                        checkCommand.Parameters.AddWithValue("@ItemId", itemId);
                        int count = Convert.ToInt32(checkCommand.ExecuteScalar());

                        if (count > 0)
                        {
                            return "Item is already recommended.";
                        }
                    }
                    string insertQuery = @"INSERT INTO Recommendation (ItemId, SentimentId)
                                   VALUES (@ItemId, @SentimentId)";
                    MySqlCommand command = new MySqlCommand(insertQuery, connection);
                    command.Parameters.AddWithValue("@ItemId", itemId);
                    command.Parameters.AddWithValue("@SentimentId", sentimentId);
                    string notificationMessage = $"Chef Roll out today Menu";
                    string insertNotificationQuery = "INSERT INTO Notification (Message, NotificationDate) VALUES (@Message, @NotificationDate)";
                    using (MySqlCommand insertNotificationCmd = new MySqlCommand(insertNotificationQuery, connection))
                    {
                        insertNotificationCmd.Parameters.AddWithValue("@Message", notificationMessage);
                        insertNotificationCmd.Parameters.AddWithValue("@NotificationDate", DateTime.Now);
                        insertNotificationCmd.ExecuteNonQuery();
                    }

                    int rowsAffected = command.ExecuteNonQuery();
                    if (rowsAffected > 0)
                    {
                        return "Recommendation inserted successfully.";
                    }
                    else
                    {
                        return "Failed to insert recommendation.";
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception occurred: " + ex.Message);
                return "An error occurred while inserting recommendation.";
            }
        }

        public static int GetSentimentId(int itemId)
        {
            try
            {
                using (MySqlConnection connection = DatabaseUtility.GetConnection())
                {
                    connection.Open();
                    string query = "SELECT SentimentId FROM Sentiment WHERE ItemId = @ItemId";
                    MySqlCommand command = new MySqlCommand(query, connection);
                    command.Parameters.AddWithValue("@ItemId", itemId);

                    object result = command.ExecuteScalar();
                    if (result != null)
                    {
                        return Convert.ToInt32(result);
                    }
                    else
                    {
                        return -1; 
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception occurred: " + ex.Message);
                return -1; 
            }
        }

        public static string RecEngineGetFoodItemForNextDay(string menuType, int returnItemListSize)
        {
            var recommendedItems = new StringBuilder();
            string query = "SELECT s.ItemId, s.OverallRating, s.SentimentScore, s.VoteCount FROM Sentiment s JOIN Item i ON s.ItemId = i.ItemId " +
                           "JOIN MealType m ON i.MealTypeId = m.meal_type_id " +
                           "WHERE m.MealType = @MenuType " +
                           "ORDER BY s.OverallRating DESC, s.SentimentScore DESC, s.VoteCount DESC " +
                           "LIMIT @ReturnItemListSize";

            try
            {
                using (MySqlConnection conn = DatabaseUtility.GetConnection())
                {
                    conn.Open();
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@MenuType", menuType);
                        cmd.Parameters.AddWithValue("@ReturnItemListSize", returnItemListSize);

                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                recommendedItems.AppendLine("\nRecommended Items:");
                                recommendedItems.AppendLine("------------------------------------------------------");
                                recommendedItems.AppendLine($"{"ItemId",-10} {"Rating",-10} {"Sentiment Score",-18} {"Votes",-10}");
                                recommendedItems.AppendLine("------------------------------------------------------");

                                while (reader.Read())
                                {
                                    recommendedItems.AppendLine(
                                        $"{reader.GetInt32("ItemId"),-10} " +
                                        $"{reader.GetFloat("OverallRating"),-10:f2} " +
                                        $"{reader.GetFloat("SentimentScore"),-18:f2} " +
                                        $"{reader.GetInt32("VoteCount"),-10}");
                                }
                            }
                            else
                            {
                                recommendedItems.AppendLine("No recommended items found.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Database exception: " + ex.Message);
                return "Failed to retrieve recommended items.";
            }

            return recommendedItems.ToString();
        }

        public static string ViewMenuItems()
        {
            try
            {
                using (MySqlConnection connection = DatabaseUtility.GetConnection())
                {
                    connection.Open();
                    string query = "SELECT i.ItemId, i.Name, i.Price, i.AvailabilityStatus, m.MealType AS MealType, i.DietPreference, i.SpiceLevel, i.FoodPreference, i.SweetTooth FROM Item i " +
                                   "INNER JOIN MealType m ON i.MealTypeId = m.meal_type_id " +
                                   "ORDER BY i.ItemId";

                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                StringBuilder result = new StringBuilder();
                                result.AppendLine("\nItems List:");
                                result.AppendLine("------------------------------------------------------------------------------------------------------------------------------");
                                result.AppendLine($"{"ID",-5} {"Name",-20} {"Price",-12} {"Availability",-15} {"Meal Type",-12} {"Diet Preference",-20} {"Spice Level",-12} {"Food Preference",-20} {"Sweet Tooth",-12}");
                                result.AppendLine("------------------------------------------------------------------------------------------------------------------------------");

                                while (reader.Read())
                                {
                                    result.AppendLine(
                                        $"{reader.GetInt32("ItemId"),-5} " +
                                        $"{reader.GetString("Name"),-20} " +
                                        $"Rs. {reader.GetDecimal("Price"),-10:f2} " +
                                        $"{(reader.GetBoolean("AvailabilityStatus") ? "True" : "False"),-15} " +
                                        $"{(reader.IsDBNull(reader.GetOrdinal("MealType")) ? "N/A" : reader.GetString("MealType")),-12}" +
                                        $"{(reader.IsDBNull(reader.GetOrdinal("DietPreference")) ? "N/A" : reader.GetString("DietPreference")),-20} " +
                                        $"{(reader.IsDBNull(reader.GetOrdinal("SpiceLevel")) ? "N/A" : reader.GetString("SpiceLevel")),-12} " +
                                        $"{(reader.IsDBNull(reader.GetOrdinal("FoodPreference")) ? "N/A" : reader.GetString("FoodPreference")),-20} " +
                                        $"{(reader.IsDBNull(reader.GetOrdinal("SweetTooth")) ? "N/A" : reader.GetString("SweetTooth")),-12}");
                            }
                                result.AppendLine("-------------------------------------------------------------------------------------------------------------------------------\n");
                                return result.ToString();
                            }
                            else
                            {
                                return "No items found";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Database exception: " + ex.Message);
                return "Failed to retrieve items";
            }
        }


        public static string ViewFeedback()
        {

            try
            {
                using (MySqlConnection connection = DatabaseUtility.GetConnection())
                {
                    connection.Open();
                    string query = "SELECT f.FeedbackId, f.UserId, f.ItemId, i.Name AS ItemName, f.Comment, f.Rating, f.FeedbackDate " +
                                   "FROM Feedback f " +
                                   "JOIN User u ON f.UserId = u.UserId " +
                                   "JOIN Item i ON f.ItemId = i.ItemId " +
                                   "WHERE f.FeedbackDate >= DATE_SUB(NOW(), INTERVAL 1 DAY)";

                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        StringBuilder feedbackList = new StringBuilder();
                        feedbackList.AppendLine("\nLast One Day Feedback:");
                        feedbackList.AppendLine("---------------------------------------------------------------------------------------------------------------------------");
                        feedbackList.AppendLine($"{"Feedback ID",-12} {"Item",-15} {"Comment",-50} {"Rating",-10} {"Date",-30}");
                        feedbackList.AppendLine("---------------------------------------------------------------------------------------------------------------------------");

                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                feedbackList.AppendLine($"{reader.GetInt32("FeedbackId"),-12} " +
                                                $"{reader.GetString("ItemName"),-15} " +
                                                $"{reader.GetString("Comment"),-50} " +
                                                $"{reader.GetInt32("Rating"),-10} " +
                                                $"{reader.GetDateTime("FeedbackDate"),-30}");
                            }
                        }
                        return feedbackList.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Database exception: " + ex.Message);
                return "Failed to retrieve recent feedback.";
            }
        }

    }

}