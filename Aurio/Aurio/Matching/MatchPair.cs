﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Aurio.Project;

namespace Aurio.Matching {
    public class MatchPair {

        public AudioTrack Track1 { get; set; }
        public AudioTrack Track2 { get; set; }
        public List<Match> Matches { get; set; }

        public double CalculateAverageSimilarity() {
            if (Matches == null || Matches.Count == 0) {
                return 0;
            }

            double similarity = 0;
            foreach (Match match in Matches) {
                similarity += match.Similarity;
            }
            return similarity /= Matches.Count;
        }

        public void SwapTracks() {
            AudioTrack temp = Track1;
            Track1 = Track2;
            Track2 = temp;
        }
    }
}