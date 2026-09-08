"""Authenticate the shared durable origin and selected producer before registry work."""

from publication import PublicationError
from publication_release_authorization import authenticate_origin


def authorized_selection(client, release, origin, selection, explicit=None):
    authenticate_origin(client, release, origin)
    payload = selection['record']['payload']
    if set(payload) != {'origin_asset_id', 'origin_sha256', 'release', 'selection'} or payload.get('release') != release or payload.get('origin_asset_id') != origin['asset_id'] or payload.get('origin_sha256') != origin['sha256']:
        raise PublicationError('Producer selection differs from its exact durable release origin')
    pinned = payload['selection']
    producer = pinned.get('producer', {})
    pair = producer.get('run_id'), producer.get('run_attempt')
    if explicit is not None and pair != explicit:
        raise PublicationError('Manual release retry conflicts with its immutable selected producer')
    return pinned
