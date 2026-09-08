"""Parse the exact CI identity carried by an artifact."""

from publication import Producer, PublicationError


def producer_from_record(record):
    if not isinstance(record, dict):
        raise PublicationError('Producer record must be an object')
    try:
        return Producer(**record)
    except TypeError:
        raise PublicationError('Producer record has missing or unexpected identity fields') from None


def invocation_sha(producer):
    return producer.source_sha


def payload_identity(producer, kind, variant):
    identity = {'schema': 1, 'repository': producer.repository, 'sha': invocation_sha(producer),
                'run_id': str(producer.run_id), 'run_attempt': str(producer.run_attempt),
                'workflow_ref': producer.workflow_ref, 'kind': kind, 'variant': variant}
    return identity
