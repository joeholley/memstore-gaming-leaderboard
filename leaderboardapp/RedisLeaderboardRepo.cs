// Copyright 2019 Google LLC

// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at

//     https://www.apache.org/licenses/LICENSE-2.0

// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
using leaderboardapp.Models;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace leaderboardapp
{
    public class RedisLeaderboardRepo : ILeaderboardRepository
    {
        private const string REDIS_HOST_ENV = "REDIS_SERVICE_HOST";
        private const string LEADERBOARD_KEY = "leaderboard";
        private const string DEFAULT_REDIS_HOST = "localhost";

        // this is thread-safe
        private ConnectionMultiplexer _redis;

        public RedisLeaderboardRepo()
        {
            _redis = ConnectionMultiplexer.Connect(GetRedisHost(),abortConnect=true,resolveDns=true);
        }

        /// <summary>
        /// RetrieveScores
        /// </summary>
        /// <param name="retrievalDetails"></param>
        /// <returns></returns>
        // [START FETCHSCORES_SERVER]
        public async Task<IList<LeaderboardItemModel>> RetrieveScores(RetrieveScoresDetails retrievalDetails)
        {
            Console.WriteLine("in RetrieveScores");
            IDatabase db = _redis.GetDatabase();
            Console.WriteLine("Attempting to connect to redis at " + GetRedisHost());
            List<LeaderboardItemModel> leaderboard = new List<LeaderboardItemModel>();

            long offset = retrievalDetails.Offset;
            long numScores = retrievalDetails.NumScores;

            // If centered, get rank of specified user first
            if (!string.IsNullOrWhiteSpace(retrievalDetails.CenterKey))
            {
                // SortedSetRankcorresponds to ZREVRANK
                Console.WriteLine("Attempting to run ZREVRANK");
                var rank = db.SortedSetRank(LEADERBOARD_KEY, retrievalDetails.CenterKey, Order.Descending);
                Console.WriteLine("Done with ZREVRANK");

                // If specified user is not present, return empty leaderboard
                if (!rank.HasValue)
                {
                    return leaderboard;
                }

                // Use rank to calculate offset
                offset = Math.Max(0, rank.Value + retrievalDetails.Offset);

                // Account for number of scores when we're attempting to center
                // at element in rank [0, abs(offset))
                if(offset <= 0)
                {
                    numScores = rank.Value + Math.Abs((long)retrievalDetails.Offset) + 1;
                }
            }

            // SortedSetRangeByScoreWithScores corresponds to ZREVRANGEBYSCORE [WITHSCORES]
            var scores = db.SortedSetRangeByScoreWithScores(LEADERBOARD_KEY,
                skip: offset,
                take: numScores,
                order: Order.Descending);

            var startingRank = offset;
            for (int i = 0; i < scores.Length; i++)
            {
                var lbItem = new LeaderboardItemModel
                {
                    Rank = startingRank++,
                    PlayerName = scores[i].Element.ToString(),
                    Score = scores[i].Score
                };
                leaderboard.Add(lbItem);
            }

            return leaderboard;
        }
        // [END FETCHSCORES_SERVER]

        /// <summary>
        /// PostScore
        /// </summary>
        /// <param name="score"></param>
        /// <returns></returns>
        // [START POSTSCORE_SERVER]
        public async Task<bool> PostScore(ScoreModel score)
        {
            Console.WriteLine("in PostScore");
            IDatabase db = _redis.GetDatabase();

            // SortedSetAdd corresponds to ZADD
            Console.WriteLine("Attempting to run ZADD");
            return db.SortedSetAdd(LEADERBOARD_KEY, score.PlayerName, score.Score);
            Console.WriteLine("Done with ZADD");
        }
        // [END POSTSCORE_SERVER]

        private string GetRedisHost()
        {
            try
            {
                var redisHost = Environment.GetEnvironmentVariable(REDIS_HOST_ENV);
                return string.IsNullOrEmpty(redisHost) ? DEFAULT_REDIS_HOST : redisHost;
            }
            catch(Exception)
            {
                return DEFAULT_REDIS_HOST;
            }
        }
    }
}
