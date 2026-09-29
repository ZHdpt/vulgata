function Assert-TestCounts {
    param([int]$Total, [int]$Passed, [int]$Failed, [int]$Skipped, [int]$ProcessExit)
    if ($ProcessExit -ne 0 -or $Failed -gt 0) { throw "Test execution failed (exit=$ProcessExit)." }
    if ($Total -eq 0 -or $Passed -eq 0) { throw 'Empty or all-skipped test selection cannot qualify a suite.' }
    if ($Total -ne ($Passed + $Failed + $Skipped)) { throw 'Incomplete or inconsistent test outcomes cannot qualify a suite.' }
}

function Assert-TestSkips {
    param([xml]$Trx)
    # Exact retained placeholders only, applied equally to every suite and focus filter.
    $allowed = @(
        'Vulgata.Tests.PreparedScanning.PreparedScanStoreRegressionTests.PreparedStore_ConcurrentIdenticalWriters_CommitOnceAndReplayOnceAsync',
        'Vulgata.Tests.RetainedSource.RetainedSourceSeparateProcessChild.CaptureAsync_WhenParentTerminatesAfterSeal_LeavesOwnedAbandonedOperation',
        'Vulgata.Tests.RetainedSource.RetainedSourceSeparateProcessChild.CaptureAsync_FromFreshProductionComposition_CompletesAndRecordsIdentity',
        'Vulgata.Tests.RetainedSource.RetainedSourceSeparateProcessChild.OpenReadContextAsync_FromFreshProductionComposition_ReadsRetainedBytesAfterRemoteUnavailable',
        'Vulgata.Tests.RetainedSource.RetainedSourceSeparateProcessChild.OpenReadContextAsync_WhenRetainedBlobIsCorrupt_DeniesWithoutContext'
    )
    $unexpected = @($Trx.TestRun.Results.UnitTestResult | Where-Object { $_.outcome -eq 'NotExecuted' -and $_.testName -cnotin $allowed })
    if ($unexpected.Count) { throw "Unexpected skips cannot qualify any suite: $($unexpected.testName -join ', ')" }
}
