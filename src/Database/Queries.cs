// Database/Queries.cs
namespace ServersList.Database;

public static class Queries
{
    public const string CreateTable =
        @"
        CREATE TABLE IF NOT EXISTS `{0}` (
            `id` INT PRIMARY KEY,
            `ip` VARCHAR(64) DEFAULT NULL,
            `name` VARCHAR(64) DEFAULT NULL,
            `map_name` VARCHAR(64) DEFAULT NULL,
            `active_players` INT DEFAULT 0,
            `max_players` INT DEFAULT 0,
            `max_players_offset` INT DEFAULT 0,
            `status` TINYINT DEFAULT 1,
            `last_update` TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
        );";

    public const string UpdateServer =
        @"
        INSERT INTO `{0}` 
        (`id`, `ip`, `name`, `active_players`, `max_players`, `map_name`, `status`)
        VALUES (@id, @ip, @name, @players, @max, @map, 1)
        ON DUPLICATE KEY UPDATE
            `ip` = VALUES(`ip`),
            `name` = VALUES(`name`),
            `active_players` = VALUES(`active_players`),
            `max_players` = VALUES(`max_players`),
            `map_name` = VALUES(`map_name`),
            `status` = 1,
            `last_update` = CURRENT_TIMESTAMP;";

    public const string SetStatus =
        @"
        INSERT INTO `{0}` (`id`, `status`, `active_players`)
        VALUES (@id, @status, 0)
        ON DUPLICATE KEY UPDATE
            `status` = @status,
            `active_players` = 0,
            `last_update` = CURRENT_TIMESTAMP;";
}
