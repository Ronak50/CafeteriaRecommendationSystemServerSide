using CafeteriaRecommendationSystem.Models;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CafeteriaRecommendationSystem.Services
{
    internal class EmployeeService
    {
        public static string EmployeeFunctionality(string action, string parameters)
        {
            switch (action.ToLower())
            {
                case "viewmenu":
                    return ViewMenu(parameters);
                case "givefeedback":
                    string[] feedbackParams = parameters.Split(';');
                    return GiveFeedback(parameters);
                case "voteitem":
                    return GiveVoteForItem(parameters);
                case "updateprofile":
                    return UpdateUserProfile(parameters);
                default:
                    return "Employee: Unknown action";
            }
        }

        public static string UpdateUserProfile(string parameters)
        {
            string[] paramParts = parameters.Split(';');
            if (paramParts.Length < 5)
            {
                return "Invalid parameters for updating profile";
            }

            int userId;
            string dietPreference = paramParts[1];
            string spicePreference = paramParts[2];
            string foodPreference = paramParts[3];
            string sweetToothPreference = paramParts[4];

            if (!int.TryParse(paramParts[0], out userId))
            {
                return "Invalid user ID";
            }

            try
            {
                using (MySqlConnection connection = DatabaseUtility.GetConnection())
                {
                    connection.Open();

                    string checkQuery = "SELECT COUNT(*) FROM UserPreference WHERE UserId = @UserId";
                    using (MySqlCommand checkCommand = new MySqlCommand(checkQuery, connection))
                    {
                        checkCommand.Parameters.AddWithValue("@UserId", userId);
                        int count = Convert.ToInt32(checkCommand.ExecuteScalar());

                        if (count == 0)
                        {
                            string insertQuery = "INSERT INTO UserPreference (UserId, DietPreference, SpiceLevel, FoodPreference, SweetTooth) VALUES (@UserId, @DietPreference, @SpiceLevel, @FoodPreference, @SweetTooth)";
                            using (MySqlCommand insertCommand = new MySqlCommand(insertQuery, connection))
                            {
                                insertCommand.Parameters.AddWithValue("@UserId", userId);
                                insertCommand.Parameters.AddWithValue("@DietPreference", dietPreference);
                                insertCommand.Parameters.AddWithValue("@SpiceLevel", spicePreference);
                                insertCommand.Parameters.AddWithValue("@FoodPreference", foodPreference);
                                insertCommand.Parameters.AddWithValue("@SweetTooth", sweetToothPreference);
                                insertCommand.ExecuteNonQuery();
                                return "Profile created successfully";
                            }
                        }
                        else
                        {
                            string updateQuery = "UPDATE UserPreference SET DietPreference = @DietPreference, SpiceLevel = @SpiceLevel, FoodPreference = @FoodPreference, SweetTooth = @SweetTooth WHERE UserId = @UserId";
                            using (MySqlCommand updateCommand = new MySqlCommand(updateQuery, connection))
                            {
                                updateCommand.Parameters.AddWithValue("@UserId", userId);
                                updateCommand.Parameters.AddWithValue("@DietPreference", dietPreference);
                                updateCommand.Parameters.AddWithValue("@SpiceLevel", spicePreference);
                                updateCommand.Parameters.AddWithValue("@FoodPreference", foodPreference);
                                updateCommand.Parameters.AddWithValue("@SweetTooth", sweetToothPreference);
                                updateCommand.ExecuteNonQuery();
                                return "Profile updated successfully";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Database exception: " + ex.Message);
                return "Failed to update profile";
            }
        }

        public static string ViewNotification()
        {
            StringBuilder notifications = new StringBuilder();
            try
            {
                using (MySqlConnection connection = DatabaseUtility.GetConnection())
                {
                    connection.Open();
                    string query = "SELECT Message FROM notification WHERE NotificationDate >= NOW() - INTERVAL 1 DAY";

                    using (MySqlCommand cmd = new MySqlCommand(query, connection))
                    {
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                notifications.AppendLine(reader["Message"].ToString());
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                  return $"An error occurred: {ex.Message}";
            }
            return notifications.Length > 0 ? notifications.ToString() : "No new notifications.";
        }

        public static string GiveVoteForItem(string parameters)
        {
            string[] feedbackParams = parameters.Split(';');
            int userId = int.Parse(feedbackParams[0]);
            int itemId = int.Parse(feedbackParams[1]);

            try
            {
                using (MySqlConnection connection = DatabaseUtility.GetConnection())
                {
                    connection.Open();
                    if (!IsItemInMenu(connection, itemId))
                    {
                        return "Given ItemId is not available in the menu, Please enter a valid Item ID";
                    }

                    string voteCheckQuery = "SELECT VoteTime FROM EmployeeVote WHERE UserId = @UserId AND VoteTime > NOW() - INTERVAL 24 HOUR";
                    using (MySqlCommand voteCheckCmd = new MySqlCommand(voteCheckQuery, connection))
                    {
                        voteCheckCmd.Parameters.AddWithValue("@UserId", userId);
                        var lastVoteTime = voteCheckCmd.ExecuteScalar() as DateTime?;
                        if (lastVoteTime.HasValue)
                        {
                            return "You have already voted in the last 24 hours. You cannot vote again at this time.";
                        }
                    }


                    string checkQuery = "SELECT COUNT(*) FROM EmployeeVote WHERE ItemId = @ItemId";

                    using (MySqlCommand checkCmd = new MySqlCommand(checkQuery, connection))
                    {
                        checkCmd.Parameters.AddWithValue("@ItemId", itemId);
                        int existingCount = Convert.ToInt32(checkCmd.ExecuteScalar());

                        if (existingCount > 0)
                        {
                            string updateQuery = "UPDATE EmployeeVote SET VoteCount = VoteCount + 1 WHERE ItemId = @ItemId";

                            using (MySqlCommand updateCmd = new MySqlCommand(updateQuery, connection))
                            {
                                updateCmd.Parameters.AddWithValue("@ItemId", itemId);

                                int updateResult = updateCmd.ExecuteNonQuery();

                                return updateResult > 0 ? "Vote successfully recorded." : "Failed to update vote count.";
                            }
                        }
                        else
                        {
                            string insertQuery = "INSERT INTO EmployeeVote (ItemId,UserId, VoteTime, VoteCount) VALUES (@ItemId, @UserId, CURRENT_TIMESTAMP, 1)";

                            using (MySqlCommand insertCmd = new MySqlCommand(insertQuery, connection))
                            {
                                insertCmd.Parameters.AddWithValue("@ItemId", itemId);
                                insertCmd.Parameters.AddWithValue("@UserId", userId);
                                int insertResult = insertCmd.ExecuteNonQuery();

                                return insertResult > 0 ? "Vote successfully recorded." : "Failed to record vote.";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Database exception: " + ex.Message);
                return "Failed to record vote.";
            }
        }

        public static string ViewMenu(string parameters)
        {
            string[] paramParts = parameters.Split(';');
            int userId;
            string mealType = paramParts[1];
            if (!int.TryParse(paramParts[0], out userId))
            {
                return "Invalid user ID";
            }

            try
            {
                using (MySqlConnection connection = DatabaseUtility.GetConnection())
                {
                    connection.Open();
                    string query = @"
                    SELECT r.RecommendationId, r.ItemId, i.Name, i.Price, i.AvailabilityStatus, 
                    s.OverallRating, s.OverallCommentSentiment, 
                    i.DietPreference, i.SpiceLevel, i.FoodPreference, i.SweetTooth
                    FROM Recommendation r
                    JOIN Item i ON r.ItemId = i.ItemId
                    LEFT JOIN Sentiment s ON i.ItemId = s.ItemId
                    JOIN MealType mt ON i.MealTypeId = mt.meal_type_id
                    JOIN UserPreference up ON up.UserId = @UserId
                    WHERE mt.MealType = @MealType
                    ORDER BY 
                    CASE WHEN up.DietPreference = i.DietPreference THEN 1 ELSE 0 END DESC, 
                    CASE WHEN up.SpiceLevel = i.SpiceLevel THEN 1 ELSE 0 END DESC, 
                    CASE WHEN up.FoodPreference = i.FoodPreference THEN 1 ELSE 0 END DESC, 
                    CASE WHEN up.SweetTooth = i.SweetTooth THEN 1 ELSE 0 END DESC";

                    using (MySqlCommand cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        cmd.Parameters.AddWithValue("@MealType", mealType);
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            StringBuilder menuList = new StringBuilder();
                            menuList.AppendLine("\nMenu Items:");
                            menuList.AppendLine("---------------------------------------------------------------------------------------------------------------------------------------------");
                            menuList.AppendLine($"{"Item ID",-10} {"Name",-15} {"Price",-10} {"Availability",-15} {"Rating",-10} {"Sentiment Comment",-20} {"Diet Preference",-15} {"Spice Level",-10} {"Food Preference",-15} {"Sweet Tooth",-10}");
                            menuList.AppendLine("---------------------------------------------------------------------------------------------------------------------------------------------");

                            while (reader.Read())
                            {
                                int itemId = reader.GetInt32("ItemId");
                                string itemName = reader.GetString("Name");
                                decimal price = reader.GetDecimal("Price");
                                bool availabilityStatus = reader.GetBoolean("AvailabilityStatus");
                                float overallRating = reader.GetFloat("OverallRating");
                                string overallCommentSentiment = reader.IsDBNull(reader.GetOrdinal("OverallCommentSentiment")) ? string.Empty : reader.GetString("OverallCommentSentiment");
                                string dietPreference = reader.IsDBNull(reader.GetOrdinal("DietPreference")) ? string.Empty : reader.GetString("DietPreference");
                                string spiceLevel = reader.IsDBNull(reader.GetOrdinal("SpiceLevel")) ? string.Empty : reader.GetString("SpiceLevel");
                                string foodPreference = reader.IsDBNull(reader.GetOrdinal("FoodPreference")) ? string.Empty : reader.GetString("FoodPreference");
                                string sweetTooth = reader.IsDBNull(reader.GetOrdinal("SweetTooth")) ? string.Empty : reader.GetString("SweetTooth");

                                menuList.AppendLine($"{itemId,-10} {itemName,-15} Rs.{price,-10} {(availabilityStatus ? "Available" : "Not Available"),-15} {overallRating,-10} {overallCommentSentiment,-20} {dietPreference,-15} {spiceLevel,-10} {foodPreference,-15} {sweetTooth,-10}");
                            }
                            menuList.AppendLine("---------------------------------------------------------------------------------------------------------------------------------------------\n");
                            return menuList.ToString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Database exception: " + ex.Message);
                return "Failed to retrieve menu.";
            }
        }

        public static bool IsItemInMenu(MySqlConnection connection, int itemId)
        {
            string query = "SELECT COUNT(*) FROM Recommendation WHERE ItemId = @ItemId";

            using (MySqlCommand cmd = new MySqlCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@ItemId", itemId);
                int count = Convert.ToInt32(cmd.ExecuteScalar());
                return count > 0;
            }
        }

        public static string GiveFeedback(string parameters)
        {
            try
            {
                string[] feedbackParams = parameters.Split(';');
                int userId = int.Parse(feedbackParams[0]);
                int itemId = int.Parse(feedbackParams[1]);
                string comment = feedbackParams[2];
                int rating = int.Parse(feedbackParams[3]);
                
                using (var connection = DatabaseUtility.GetConnection())
                {
                    connection.Open();

                    if (!IsItemInMenu(connection, itemId))
                    {
                        return "Given ItemId is not available in the menu, Please enter a valid Item ID";
                    }

                    string feedbackCheckQuery = "SELECT FeedbackDate FROM Feedback WHERE UserId = @UserId AND FeedbackDate > NOW() - INTERVAL 24 HOUR";
                    using (MySqlCommand feedbackCheckCmd = new MySqlCommand(feedbackCheckQuery, connection))
                    {
                        feedbackCheckCmd.Parameters.AddWithValue("@UserId", userId);
                        var lastFeedbackTime = feedbackCheckCmd.ExecuteScalar() as DateTime?;
                        if (lastFeedbackTime.HasValue)
                        {
                            return "You have already give feedback in the last 24 hours. You cannot give feedback again at this time.";
                        }
                    }

                    (string sentimentComment, float sentimentScore, string commentSentiments) = CalculateSentimentScore(comment);
                    var sentimentData = GetSentimentData(connection, itemId);

                    InsertFeedback(connection, userId, itemId, comment, rating);

                    int voteCount = UpdateVoteCount(connection, itemId);

                    if (sentimentData.HasValue)
                    {
                        UpdateSentimentData(connection, itemId, rating, sentimentScore, commentSentiments, sentimentData.Value, voteCount);
                    }
                    else
                    {
                        InsertNewSentimentData(connection, itemId, rating, sentimentComment, sentimentScore, commentSentiments, voteCount);
                    }

                    return "Feedback submitted successfully.";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Database exception: " + ex.Message);
                return "Failed to submit feedback.";
            }
        }

        private static (int sentimentId, double existingOverallRating, double existingSentimentScore, string existingcommentSentiments)? GetSentimentData(MySqlConnection connection, int itemId)
        {
            const string query = "SELECT SentimentId, OverallRating, SentimentScore, CommentSentiments FROM Sentiment WHERE ItemId = @ItemId";
            using (var cmd = new MySqlCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@ItemId", itemId);
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        int sentimentId = reader.GetInt32("SentimentId");
                        double existingOverallRating = reader["OverallRating"] != DBNull.Value ? Convert.ToDouble(reader["OverallRating"]) : 0.0;
                        double existingSentimentScore = reader["SentimentScore"] != DBNull.Value ? Convert.ToDouble(reader["SentimentScore"]) : 0.0;
                        string existingCommentSentiments = reader["CommentSentiments"] != DBNull.Value ? reader["CommentSentiments"].ToString() : string.Empty;

                        return (sentimentId, existingOverallRating, existingSentimentScore, existingCommentSentiments);
                    }
                }
            }
            return null;
        }

        private static int UpdateVoteCount(MySqlConnection connection, int itemId)
        {
            const string query = "SELECT VoteCount FROM Sentiment WHERE ItemId = @ItemId";
            using (var cmd = new MySqlCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@ItemId", itemId);
                var result = cmd.ExecuteScalar();
                return result != null && result != DBNull.Value ? Convert.ToInt32(result) + 1 : 1;
            }
        }
        private static void InsertFeedback(MySqlConnection connection, int userId, int itemId, string comment, int rating)
        {
            const string query = "INSERT INTO Feedback (UserId, ItemId, Comment, Rating, FeedbackDate) VALUES (@UserId, @ItemId, @Comment, @Rating, NOW())";
            using (var cmd = new MySqlCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@ItemId", itemId);
                cmd.Parameters.AddWithValue("@Comment", comment);
                cmd.Parameters.AddWithValue("@Rating", rating);
                cmd.ExecuteNonQuery();
            }
        }

        private static void UpdateSentimentData(MySqlConnection connection, int itemId, int rating, float sentimentScore, string commentSentiments, (int sentimentId, double existingOverallRating, double existingSentimentScore, string existingCommentSentiments) sentimentData, int voteCount)
        {
            float overallSentimentScore = (float)((sentimentData.existingSentimentScore + sentimentScore) / 2.0);
            float overallRating = (float)((sentimentData.existingOverallRating + rating) / 2.0);

            string sentimentComment;
            if (overallSentimentScore > 0)
            {
                sentimentComment = "Positive";
            }
            else if (overallSentimentScore < 0)
            {
                sentimentComment = "Negative";
            }
            else
            {
                sentimentComment = "Neutral";
            }

            string updatedCommentSentiments = !string.IsNullOrEmpty(sentimentData.existingCommentSentiments)? $"{sentimentData.existingCommentSentiments}, {commentSentiments}" : commentSentiments;

            const string query = "UPDATE Sentiment SET OverallRating = @OverallRating, OverallCommentSentiment = @OverallCommentSentiment, SentimentScore = @SentimentScore, VoteCount = @VoteCount, CommentSentiments = @CommentSentiments WHERE ItemId = @ItemId";
            using (var cmd = new MySqlCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@OverallRating", overallRating);
                cmd.Parameters.AddWithValue("@OverallCommentSentiment", sentimentComment);
                cmd.Parameters.AddWithValue("@SentimentScore", overallSentimentScore);
                cmd.Parameters.AddWithValue("@ItemId", itemId);
                cmd.Parameters.AddWithValue("@VoteCount", voteCount);
                cmd.Parameters.AddWithValue("@CommentSentiments", updatedCommentSentiments);
                cmd.ExecuteNonQuery();
            }
        }

        private static void InsertNewSentimentData(MySqlConnection connection, int itemId, int rating, string sentimentComment, float sentimentScore, string commentSentiments, int voteCount)
        {
            const string query = "INSERT INTO Sentiment (ItemId, OverallRating, OverallCommentSentiment, SentimentScore, VoteCount, CommentSentiments) VALUES (@ItemId, @OverallRating, @OverallCommentSentiment, @SentimentScore, @VoteCount, @CommentSentiments)";
            using (var cmd = new MySqlCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@ItemId", itemId);
                cmd.Parameters.AddWithValue("@OverallRating", rating);
                cmd.Parameters.AddWithValue("@OverallCommentSentiment", sentimentComment);
                cmd.Parameters.AddWithValue("@SentimentScore", sentimentScore);
                cmd.Parameters.AddWithValue("@CommentSentiments", commentSentiments);
                cmd.Parameters.AddWithValue("@VoteCount", voteCount);
                cmd.ExecuteNonQuery();
            }
        }

        public static (string sentiment, float sentimentScore, string commentSentiments) CalculateSentimentScore(string comment)
        {
            try
            {
                var positiveWords = File.ReadAllLines(@"C:\Users\ronak.sharma\source\repos\CafeteriaRecommendationSystem\CafeteriaRecommendationSystem\Data\positive_words.txt");
                var negativeWords = File.ReadAllLines(@"C:\Users\ronak.sharma\source\repos\CafeteriaRecommendationSystem\CafeteriaRecommendationSystem\Data\negative_words.txt");
                var negationWords = new string[] { "not", "never", "no", "nothing", "neither" };
                comment = comment.ToLower();
                var words = comment.Split(' ');

                int sentimentScore = 0;
                bool isNegation = false;
                List<string> matchedWords = new List<string>();

                for (int i = 0; i < words.Length; i++)
                {
                    if (negationWords.Contains(words[i]))
                    {
                        isNegation = true;
                        continue;
                    }

                    if (positiveWords.Contains(words[i]))
                    {
                        sentimentScore += isNegation ? -1 : 1;
                        isNegation = false;
                        matchedWords.Add(words[i]);
                    }
                    else if (negativeWords.Contains(words[i]))
                    {
                        sentimentScore += isNegation ? 1 : -1;
                        isNegation = false;
                        matchedWords.Add(words[i]);
                    }
                }

                string sentiment = sentimentScore > 0 ? "Positive" : sentimentScore < 0 ? "Negative" : "Neutral";
                string commentSentiments = string.Join(", ", matchedWords);
                return (sentiment, sentimentScore, commentSentiments);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error calculating sentiment score: " + ex.Message);
                return (string.Empty, 0, string.Empty);
            }
        }
    }
}