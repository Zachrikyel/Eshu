using System;
using System.Collections.Generic;
using System.Linq;
using Eshu.Models;

namespace Eshu.Services
{
    // El "cerebro" de la recomendación: nunca excluye, solo puntúa. Un juego que
    // no calza con ninguna respuesta compite con desventaja, no desaparece de la
    // lista — así un catálogo chico en un género igual recibe una recomendación.
    public class RecommendationEngine
    {
        public enum Transition { Continuity, RadicalChange }
        public enum Pillar { Story, Action, Strategy, Casual }
        public enum Commitment { Short, Standard, LongHaul }
        public enum Friction { Chill, Balanced, Maximum }

        // Heurística de partida para mapear géneros de IGDB a los 4 pilares —
        // no es ciencia exacta, se puede ajustar según lo que veamos en la práctica.
        private static readonly Dictionary<string, Pillar> GenreToPillar = new()
        {
            ["Role-playing (RPG)"] = Pillar.Story,
            ["Adventure"] = Pillar.Story,
            ["Visual Novel"] = Pillar.Story,
            ["Point-and-click"] = Pillar.Story,
            ["Shooter"] = Pillar.Action,
            ["Fighting"] = Pillar.Action,
            ["Hack and slash/Beat 'em up"] = Pillar.Action,
            ["Platform"] = Pillar.Action,
            ["Arcade"] = Pillar.Action,
            ["Strategy"] = Pillar.Strategy,
            ["Turn-based strategy (TBS)"] = Pillar.Strategy,
            ["Real Time Strategy (RTS)"] = Pillar.Strategy,
            ["Tactical"] = Pillar.Strategy,
            ["Simulator"] = Pillar.Strategy,
            ["MOBA"] = Pillar.Strategy,
            ["Puzzle"] = Pillar.Casual,
            ["Card & Board Game"] = Pillar.Casual,
            ["Music"] = Pillar.Casual,
            ["Quiz/Trivia"] = Pillar.Casual,
            ["Pinball"] = Pillar.Casual,
            ["Sport"] = Pillar.Casual,
            ["Racing"] = Pillar.Casual,
        };

        public List<Game> Recommend(
            IEnumerable<Game> library,
            string? lastCompletedGenre,
            Transition transition,
            Pillar pillar,
            Commitment commitment,
            Friction friction,
            int topN = 10)
        {
            var candidates = library.Where(g => g.Status != GameStatus.Completed);

            return candidates
                .Select(game => new { Game = game, Score = ScoreGame(game, lastCompletedGenre, transition, pillar, commitment, friction) })
                .OrderByDescending(x => x.Score)
                .Take(topN)
                .Select(x => x.Game)
                .ToList();
        }

        private int ScoreGame(Game game, string? lastCompletedGenre, Transition transition, Pillar pillar, Commitment commitment, Friction friction)
        {
            int score = 0;

            // 1. Efecto rebote — nunca excluye, solo suma o resta.
            if (!string.IsNullOrWhiteSpace(lastCompletedGenre) && !string.IsNullOrWhiteSpace(game.Genre))
            {
                bool sameGenre = game.Genre == lastCompletedGenre;
                if (transition == Transition.Continuity && sameGenre) score += 3;
                if (transition == Transition.RadicalChange && sameGenre) score -= 3;
            }

            // 2. Pilar de la experiencia.
            if (!string.IsNullOrWhiteSpace(game.Genre) && GenreToPillar.TryGetValue(game.Genre, out var gamePillar))
            {
                if (gamePillar == pillar) score += 4;
            }

            // 3. Compromiso de tiempo — sin EstimatedHoursToBeat poblado todavía
            // (ver README, Parte 7), esto no suma ni resta para ningún juego aún.
            if (game.EstimatedHoursToBeat > 0)
            {
                bool matches = commitment switch
                {
                    Commitment.Short => game.EstimatedHoursToBeat <= 12,
                    Commitment.Standard => game.EstimatedHoursToBeat is > 12 and <= 30,
                    Commitment.LongHaul => game.EstimatedHoursToBeat > 30,
                    _ => false
                };
                if (matches) score += 2;
            }

            // 4. Fricción — sin Difficulty poblado todavía, tampoco diferencia aún.
            if (game.Difficulty.HasValue)
            {
                int target = friction switch
                {
                    Friction.Chill => 1,
                    Friction.Balanced => 3,
                    Friction.Maximum => 5,
                    _ => 3
                };
                score -= Math.Abs(game.Difficulty.Value - target);
            }

            return score;
        }
    }
}
