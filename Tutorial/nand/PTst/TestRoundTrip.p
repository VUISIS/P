
type tTestBuffer = (buffer: seq[int], len: int);

type tTestReadWrite = (blockAddress: int, pageAddress: int, byteAddress: int, len: int);
type tTestReadWriteResp = int;

type tRoundTripInit = machine;

event eTestingWrite: tTestBuffer;
event eTestingRead: tTestBuffer;

event eTestReadWrite : tTestReadWrite;
event eTestReadWriteResp : tTestReadWriteResp;

machine TestRoundTrip {
    var testSendBuffer: seq[int];
    var testSendLen: int;
    var tester: machine;
    var client: machine;
    var blockAddress: int;
    var pageAddress: int;
    var byteAddress: int;

    start state Init {
        entry (init: tRoundTripInit) {
            tester = init;
        }

        on eRegisterClient do (clientRef: tRegisterClient) {
            client = clientRef;
            send tester, eRegisterClient, this;
        }

        on eRegisterClientResp do {
            send client, eRegisterClientResp;
            goto awaitRequest;
        }
    }

    state awaitRequest {
        on eTestReadWrite do (req: tTestReadWrite) {
            var sendBuff: seq[int];
            var writeTest: tWriteTestRequest;
            blockAddress = req.blockAddress;
            pageAddress = req.pageAddress;
            byteAddress = req.byteAddress;
            testSendLen = 0;
            while (testSendLen < req.len) {
                sendBuff += (testSendLen, choose(256));
                testSendLen = testSendLen + 1;
            }
            testSendBuffer = sendBuff;
            writeTest = (blockAddress=req.blockAddress, pageAddress=req.pageAddress, byteAddress=req.byteAddress, buffer = sendBuff, len=testSendLen);
            send tester, eWriteTestRequest, writeTest;
            goto awaitWriteResponse;
        }
    }

    state awaitWriteResponse {
        on eWriteTestResponse do (resp: tWriteTestResponse) {
            var readReq : tReadTestRequest;
            if (resp < 0) {
                send client, eTestReadWriteResp, -1;
                goto awaitRequest;
            }
            readReq = (blockAddress=blockAddress, pageAddress=pageAddress, byteAddress=byteAddress, len=testSendLen);
            send tester, eReadTestRequest, readReq;
            goto awaitReadResponse;
        }
    }

    state awaitReadResponse {
        on eReadTestResponse do (resp: tReadTestResponse) {
            send client, eTestReadWriteResp, resp.len;
            goto awaitRequest;
        }
    }
}
