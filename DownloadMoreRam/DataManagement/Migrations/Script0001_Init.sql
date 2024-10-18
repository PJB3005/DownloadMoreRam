CREATE TABLE DownloadLog(
    DownloadLogId INTEGER PRIMARY KEY,
    Time DATETIME NOT NULL,
    AmountMB INTEGER NOT NULL
);

CREATE INDEX DownloadLog_Time ON DownloadLog(Time);
