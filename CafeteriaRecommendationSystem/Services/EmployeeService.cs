using CafeteriaRecommendationSystem.Models;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
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
                default:
                    return "Employee: Unknown action";
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

        public static string GiveVoteForItem(string itemIdParam)
        {
            int itemId;
            if (!int.TryParse(itemIdParam, out itemId))
            {
                return "Invalid itemId. It should be an integer.";
            }

            try
            {
                using (MySqlConnection connection = DatabaseUtility.GetConnection())
                {
                    connection.Open();
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
                            string insertQuery = "INSERT INTO EmployeeVote (ItemId, VoteTime, VoteCount) VALUES (@ItemId, CURRENT_TIMESTAMP, 1)";

                            using (MySqlCommand insertCmd = new MySqlCommand(insertQuery, connection))
                            {
                                insertCmd.Parameters.AddWithValue("@ItemId", itemId);

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


        public static string ViewMenu(string mealType)
            {
            try
            {
                using (MySqlConnection connection = DatabaseUtility.GetConnection())
                {
                    connection.Open();
                    string query = @"
                    SELECT r.RecommendationId, r.ItemId, i.Name, i.Price, i.AvailabilityStatus, s.OverallRating, s.OverallCommentSentiment
                    FROM Recommendation r
                    JOIN Item i ON r.ItemId = i.ItemId
                    LEFT JOIN Sentiment s ON i.ItemId = s.ItemId
                    JOIN MealType mt ON i.MealTypeId = mt.meal_type_id
                    WHERE mt.type = @MealType";

                    using (MySqlCommand cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@MealType", mealType);
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {

                            if (reader.Read())
                            {
                                int recommendationId = reader.GetInt32("RecommendationId");
                                int itemId = reader.GetInt32("ItemId");
                                string itemName = reader.GetString("Name");
                                decimal price = reader.GetDecimal("Price");
                                bool availabilityStatus = reader.GetBoolean("AvailabilityStatus");
                                float overallRating = reader.GetFloat("OverallRating");
                                string OverallCommentSentiment = reader.IsDBNull(reader.GetOrdinal("OverallCommentSentiment")) ? string.Empty : reader.GetString("OverallCommentSentiment");
                                string priceString = $"Rs.{reader.GetDecimal("Price")}";
                                string formattedLine = $"\nRecommendation ID: {recommendationId}, Item ID: {itemId}, Name: {itemName}, Price: {priceString}, Availability: {(availabilityStatus ? "Available" : "Not Available")}, Overall Rating: {overallRating}, Sentiment Comment: {OverallCommentSentiment}";

                                return formattedLine;
                            }
                            else
                            {
                                return "No recommendations found for the specified meal type.";
                            }
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

        public static string GiveFeedback(string parameters)
        {
            try
            {
                string[] feedbackParams = parameters.Split(';');
                int userId = int.Parse(feedbackParams[0]);
                int itemId = int.Parse(feedbackParams[1]);
                string comment = feedbackParams[2];
                int rating = int.Parse(feedbackParams[3]);

                (string sentimentComment, float sentimentScore, string commentSentiments) = CalculateSentimentScore(comment);
                using (MySqlConnection connection = DatabaseUtility.GetConnection())
                {
                        connection.Open();

                        bool itemExists = false;
                        string checkItemQuery = "SELECT COUNT(*) FROM item WHERE ItemId = @ItemId";
                        using (MySqlCommand checkCmd = new MySqlCommand(checkItemQuery, connection))
                        {
                            checkCmd.Parameters.AddWithValue("@ItemId", itemId);
                            itemExists = Convert.ToInt32(checkCmd.ExecuteScalar()) > 0;
                        }

                        if (!itemExists)
                        {
                            return "Error: ItemId does not exist in the item table.";
                        }

                        string selectQuery = "SELECT SentimentId,OverallRating,SentimentScore FROM Sentiment WHERE ItemId = @ItemId";
                        MySqlCommand selectCmd = new MySqlCommand(selectQuery, connection);
                        selectCmd.Parameters.AddWithValue("@ItemId", itemId);
                        MySqlDataReader reader = selectCmd.ExecuteReader();

                        double existingOverallRating = 0.0;
                        double existingSentimentScore = 0.0;
                        int sentimentId = 0;
                        if (reader.Read())
                        {
                            sentimentId = reader.GetInt32("SentimentId");
                            if (!reader["OverallRating"].Equals(DBNull.Value))
                            {
                                existingOverallRating = Convert.ToDouble(reader["OverallRating"]);
                            }

                            if (!reader["SentimentScore"].Equals(DBNull.Value))
                            {
                                existingSentimentScore = Convert.ToDouble(reader["SentimentScore"]);
                            }
                        }
                        reader.Close();

                   

                    string feedbackQuery = "INSERT INTO Feedback (UserId, ItemId, Comment, Rating, FeedbackDate) VALUES (@UserId, @ItemId, @Comment, @Rating, NOW())";
                        using (MySqlCommand insertCmd = new MySqlCommand(feedbackQuery, connection))
                        {
                            insertCmd.Parameters.AddWithValue("@UserId", userId);
                            insertCmd.Parameters.AddWithValue("@ItemId", itemId);
                            insertCmd.Parameters.AddWithValue("@Comment", comment);
                            insertCmd.Parameters.AddWithValue("@Rating", rating);
                            insertCmd.ExecuteNonQuery();
                        }

                    string votedItemsQuery = "INSERT INTO voteditem (UserId, ItemId, VoteDate) VALUES (@UserId, @ItemId, NOW())";
                    using (MySqlCommand insertCmd = new MySqlCommand(votedItemsQuery, connection))
                    {
                        insertCmd.Parameters.AddWithValue("@UserId", userId);
                        insertCmd.Parameters.AddWithValue("@ItemId", itemId);
                        insertCmd.ExecuteNonQuery();
                    }
                    int voteCount = 1;
                    string countQuery = "SELECT VoteCount FROM Sentiment WHERE ItemId = @ItemId";
                    using (MySqlCommand countCmd = new MySqlCommand(countQuery, connection))
                    {
                        countCmd.Parameters.AddWithValue("@ItemId", itemId);
                        var result = countCmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            voteCount = Convert.ToInt32(result) + 1;
                        }
                    }

                    float overallSentimentScore = 0, overallRating = 0;
                    string existingCommentSentiments = string.Empty;
                    if (sentimentId != 0)
                        {
                        string fetchCommentSentimentsQuery = "SELECT CommentSentiments FROM Sentiment WHERE ItemId = @ItemId";
                        using (MySqlCommand fetchCmd = new MySqlCommand(fetchCommentSentimentsQuery, connection))
                        {
                            fetchCmd.Parameters.AddWithValue("@ItemId", itemId);
                            existingCommentSentiments = fetchCmd.ExecuteScalar()?.ToString() ?? string.Empty;
                        }
                        if (!string.IsNullOrEmpty(existingCommentSentiments))
                        {
                            existingCommentSentiments += ", " + commentSentiments;
                        }
                        else
                        {
                            existingCommentSentiments = commentSentiments;
                        }
                        overallSentimentScore = (float)((existingSentimentScore + sentimentScore) / 2.0);
                            overallRating = (float)((existingOverallRating + rating) / 2.0);
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
                        string updateQuery = "UPDATE Sentiment SET OverallRating = @OverallRating, OverallCommentSentiment = @OverallCommentSentiment, SentimentScore = @SentimentScore, VoteCount = @VoteCount, CommentSentiments = @CommentSentiments WHERE ItemId = @ItemId";
                        using (MySqlCommand updateCmd = new MySqlCommand(updateQuery, connection))
                        {
                            updateCmd.Parameters.AddWithValue("@OverallRating", overallRating);
                            updateCmd.Parameters.AddWithValue("@OverallCommentSentiment", sentimentComment);
                            updateCmd.Parameters.AddWithValue("@SentimentScore", overallSentimentScore);
                            updateCmd.Parameters.AddWithValue("@ItemId", itemId);
                            updateCmd.Parameters.AddWithValue("@VoteCount", voteCount);
                            updateCmd.Parameters.AddWithValue("@CommentSentiments", existingCommentSentiments);
                            updateCmd.ExecuteNonQuery();
                        }
                        }
                        else
                        {
                            string insertQuery = "INSERT INTO Sentiment (ItemId, OverallRating, OverallCommentSentiment, SentimentScore, VoteCount,CommentSentiments) VALUES (@ItemId, @OverallRating, @OverallCommentSentiment, @SentimentScore,@VoteCount,@CommentSentiments)";
                            using (MySqlCommand insertCmd = new MySqlCommand(insertQuery, connection))
                            {
                                insertCmd.Parameters.AddWithValue("@ItemId", itemId);
                                insertCmd.Parameters.AddWithValue("@OverallRating", rating);
                                insertCmd.Parameters.AddWithValue("@OverallCommentSentiment", sentimentComment);
                                insertCmd.Parameters.AddWithValue("@SentimentScore", sentimentScore);
                                insertCmd.Parameters.AddWithValue("@CommentSentiments", commentSentiments);
                                insertCmd.Parameters.AddWithValue("@VoteCount", voteCount);
                                insertCmd.ExecuteNonQuery();
                            }
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