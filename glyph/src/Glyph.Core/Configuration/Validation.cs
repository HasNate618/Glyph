using System;
using System.Collections.Generic;
using System.Linq;

namespace Glyph.Core.Configuration
{
    public static class Validation
    {
        public static List<string> ValidateConfig(ConfigModel config)
        {
            var errors = new List<string>();

            if (config == null)
            {
                errors.Add("Configuration cannot be null.");
                return errors;
            }

            if (string.IsNullOrWhiteSpace(config.LeaderKey))
            {
                errors.Add("Leader key must be defined.");
            }

            if (config.Actions == null || !config.Actions.Any())
            {
                errors.Add("At least one action must be defined.");
            }
            else
            {
                foreach (var action in config.Actions)
                {
                    var actionErrors = ValidateAction(action);
                    errors.AddRange(actionErrors);
                }
            }

            return errors;
        }

        private static List<string> ValidateAction(ActionModel action)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(action.Name))
            {
                errors.Add("Action name must be defined.");
            }

            if (string.IsNullOrWhiteSpace(action.Type))
            {
                errors.Add("Action type must be defined.");
            }

            // Additional validation based on action type can be added here

            return errors;
        }
    }
}