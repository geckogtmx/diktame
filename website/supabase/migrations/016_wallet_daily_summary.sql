-- Daily aggregated wallet summary for dashboard display.
-- Groups USAGE/SYNC rows by day, shows GRANT/PURCHASE/REFUND/EXPIRY individually.
-- Returns credits (microdollars / 1000) instead of raw microdollars.

CREATE OR REPLACE FUNCTION get_wallet_daily_summary(p_user_id UUID, p_max_days INTEGER DEFAULT 60)
RETURNS TABLE (
    day_date       DATE,
    day_label      TEXT,
    type           TEXT,
    credits        BIGINT,
    balance_credits BIGINT,
    request_count  INTEGER,
    is_aggregate   BOOLEAN
)
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
BEGIN
    RETURN QUERY
    WITH individual AS (
        -- Non-usage events shown individually
        SELECT
            (created_at AT TIME ZONE 'UTC')::date AS d,
            wl.type AS t,
            wl.amount_micro / 1000 AS cr,
            wl.balance_after_micro / 1000 AS bal,
            1 AS cnt,
            FALSE AS agg,
            wl.created_at AS sort_ts
        FROM wallet_ledger wl
        WHERE wl.user_id = p_user_id
          AND wl.type IN ('GRANT', 'PURCHASE', 'REFUND', 'EXPIRY')
    ),
    daily_usage AS (
        -- USAGE + SYNC rows grouped by day
        SELECT
            (created_at AT TIME ZONE 'UTC')::date AS d,
            'DAILY'::text AS t,
            SUM(wl.amount_micro)::bigint / 1000 AS cr,
            -- Balance = the last balance_after_micro of the day
            (ARRAY_AGG(wl.balance_after_micro ORDER BY wl.created_at DESC))[1]::bigint / 1000 AS bal,
            COUNT(*)::integer AS cnt,
            TRUE AS agg,
            MAX(wl.created_at) AS sort_ts
        FROM wallet_ledger wl
        WHERE wl.user_id = p_user_id
          AND wl.type IN ('USAGE', 'SYNC')
        GROUP BY (created_at AT TIME ZONE 'UTC')::date
        HAVING SUM(wl.amount_micro) != 0  -- skip days with zero net change
    ),
    combined AS (
        SELECT * FROM individual
        UNION ALL
        SELECT * FROM daily_usage
    )
    SELECT
        c.d AS day_date,
        CASE
            WHEN c.d = CURRENT_DATE THEN 'Today'
            WHEN c.d = CURRENT_DATE - 1 THEN 'Yesterday'
            ELSE TO_CHAR(c.d, 'Mon DD')
        END AS day_label,
        c.t AS type,
        c.cr AS credits,
        c.bal AS balance_credits,
        c.cnt AS request_count,
        c.agg AS is_aggregate
    FROM combined c
    ORDER BY c.sort_ts DESC
    LIMIT p_max_days;
END;
$$;
