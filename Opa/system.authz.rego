package system.authz

default allow := false

# OPA exposes the Authorization bearer token as input.identity.
claims := payload if {
    [valid, header, payload] := io.jwt.decode_verify(input.identity, {
        "secret": opa.runtime().config.service_auth.signing_key,
        "alg": "HS256",
        "iss": "authorization-sample-services",
        "aud": "opa",
    })
    valid
    header.alg == "HS256"
    is_number(payload.exp)
}

allow if {
    input.method == "GET"
    input.path == ["health"]
}

# API1 can evaluate its decisions, but cannot read or change policy/data.
allow if {
    claims.client_id == "api1"
    input.method == "POST"
    input.path in {
        ["v1", "data", "api1", "make_transfer_higher_limit_v1", "allow"],
        ["v1", "data", "api1", "make_transfer_higher_limit_v2", "allow"],
    }
}

# The sample IAM identity can maintain only API1 role assignments.
allow if {
    claims.client_id == "iam"
    input.method in {"GET", "PUT"}
    array.slice(input.path, 0, 6) == ["v1", "data", "authorization", "services", "api1", "roles"]
}
