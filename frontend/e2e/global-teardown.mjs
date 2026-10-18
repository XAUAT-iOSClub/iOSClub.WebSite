import {DatabaseSync} from 'node:sqlite'
import {cleanup, DB_PATH} from './global-setup.mjs'

export default async function globalTeardown() {
    const db = new DatabaseSync(DB_PATH)
    try {
        cleanup(db)
    } finally {
        db.close()
    }
}
