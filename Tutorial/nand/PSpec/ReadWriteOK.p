spec ReadWriteOK observes eWriteTestRequest, eWriteTestResponse, eReadTestResponse {
    var lastWriteBuffer: seq[int];
    var lastWriteLen: int;

    start state Init {
        entry {
            goto watchRequests;
        }
    }

    state watchRequests {
        on eWriteTestRequest do (req: tWriteTestRequest) {
            lastWriteBuffer = req.buffer;
            lastWriteLen = req.len;
            goto awaitWriteResponse;
        }
    }

    hot state awaitWriteResponse {
        on eWriteTestResponse do (resp: tWriteTestResponse) {
            assert resp >= 0,
                format ("write request got error response {0}", resp);
            goto awaitReadResponse;
        }
    }

    hot state awaitReadResponse {
        on eReadTestResponse do (resp: tReadTestResponse) {
            var i: int;
            assert resp.len >= 0,
                format ("read request got error response {0}", resp.len);
            assert resp.len == lastWriteLen,
                format("number of bytes read ({0}) != number of bytes written ({1})", resp.len, lastWriteLen);
            i = 0;
            while (i < resp.len) {
                assert resp.buffer[i] == lastWriteBuffer[i],
                    format("read buffer does not match write buffer at location {0}, {1} != {2}", i, resp.buffer[i], lastWriteBuffer[i]);
                i = i + 1;
            }
            goto watchRequests;
        }
    }
}
