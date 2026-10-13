package api1.make_transfer_higher_limit_v1_test

import data.api1.make_transfer_higher_limit_v1 as transfers

transfer_input(amount, tasks) := {
    "allowedTasks": tasks,
    "resource": {"amount": amount},
}

test_standard_limit_allowed if {
    transfers.allow with input as transfer_input(1000, ["api1:make-transfer"])
}

test_above_standard_limit_denied if {
    not transfers.allow with input as transfer_input(1000.01, ["api1:make-transfer"])
}

test_higher_limit_allowed if {
    transfers.allow with input as transfer_input(2000, ["api1:make-transfer", "api1:make-transfer-higher-limit"])
}

test_unexpanded_grant_list_does_not_satisfy_base_requirement if {
    not transfers.allow with input as transfer_input(2000, ["api1:make-transfer-higher-limit"])
}

test_no_tasks_denied if {
    not transfers.allow with input as transfer_input(100, [])
}

test_nonpositive_amount_denied if {
    not transfers.allow with input as transfer_input(0, ["api1:make-transfer"])
}

test_input_service_cannot_skip_the_amount_rule if {
    request := object.union(transfer_input(2000, ["api1:make-transfer"]), {"service": "another-api"})
    not transfers.allow with input as request
}

# Selecting the extended policy does not require its task grant below the limit.
test_standard_amount_does_not_need_override_grant if {
    transfers.allow with input as transfer_input(100, ["api1:make-transfer"])
}

test_input_task_cannot_skip_the_amount_rule if {
    request := object.union(transfer_input(2000, ["api1:make-transfer"]), {"task": "api1:make-transfer"})
    not transfers.allow with input as request
}

test_standard_limit_comes_from_data if {
    not transfers.allow with input as transfer_input(51, ["api1:make-transfer"])
        with data.authorization.services.api1.standardLimit as 50
    transfers.allow with input as transfer_input(50, ["api1:make-transfer"])
        with data.authorization.services.api1.standardLimit as 50
    transfers.allow with input as transfer_input(51, ["api1:make-transfer", "api1:make-transfer-higher-limit"])
        with data.authorization.services.api1.standardLimit as 50
}
