spec RoundTripOK observes eTestReadWrite, eWriteTestRequest, eWriteTestResponse, eReadTestRequest, eReadTestResponse {
    var lastWriteBuffer: seq[int];
    var lastWriteLen: int;

    start state Init {
        entry {
            goto watchRequests;
        }
    }

    state watchRequests {
        on eTestReadWrite do (req: tTestReadWrite) {
            goto awaitWrite;
        }
    }

    hot state awaitWrite {
        on eWriteTestRequest do (req: tWriteTestRequest) {
            goto awaitWriteResp;
        }
    }

    hot state awaitWriteResp {
        on eWriteTestResponse do (resp: tWriteTestResponse) {
            goto awaitRead;
        }
    }

    hot state awaitRead {
        on eReadTestRequest do (req: tReadTestRequest) {
            goto awaitReadResp;
        }
    }

    hot state awaitReadResp {
        on eReadTestResponse do (req: tReadTestResponse) {
            goto watchRequests;
        }
    }
}
