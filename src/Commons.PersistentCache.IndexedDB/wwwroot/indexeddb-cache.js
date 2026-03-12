// indexeddb-cache.js - IndexedDB cache operations for Blazor WASM

const dbVersion = 1;
const configurationKey = '__CACHE_CONFIG__';
const databases = {};
const configurations = {};

/**
 * Initialize the IndexedDB database with object stores and indexes
 */
export async function initializeDatabase(cacheName) {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(cacheName, dbVersion);

        request.onerror = () => {
            console.error(`Failed to open database: ${request.error}`);
            resolve(null);
        };

        request.onsuccess = () => {
            databases[cacheName] = request.result;
            configurations[cacheName] = getConfigurationForCache(cacheName);
            resolve(getTotalSize(cacheName));
        };

        request.onupgradeneeded = (e) => {
            const db = e.target.result;

            if (!db.objectStoreNames.contains('cacheStore')) {
                const store = db.createObjectStore('cacheStore', { keyPath: 'key' });
                store.createIndex('createdAt', 'createdAt', { unique: false });
                store.createIndex('lastAccessedAt', 'lastAccessedAt', { unique: false });
            }
            
            if (!db.objectStoreNames.contains('configurationStore')) {
                const store = db.createObjectStore('configurationStore', { keyPath: 'key' });
            }
        };
    });
}

/**
 * Get the database connection
 */
function getDatabase(cacheName) {
    return databases[cacheName];
}

function getConfigurationForCache(cacheName) {
    return configurations[cacheName];
}

/**
 * Save global configuration
 */
export async function saveConfiguration(cacheName, configuration) {
    try {
        const db = getDatabase(cacheName);
        const tx = db.transaction(['configurationStore'], 'readwrite');
        const store = tx.objectStore('configurationStore');

        const entry = {
            key: configurationKey,
            data: configuration,
        };

        store.put(entry);

        return new Promise((resolve, reject) => {
            tx.oncomplete = () =>
            {
                configurations[cacheName] = configuration;
                resolve(true);
            }
            tx.onerror = () => {
                console.error('Save configuration error:', tx.error);
                resolve(false);
            }
        });
    } catch (error) {
        console.error('Save configuration error:', error);
        return false;
    }
}

/**
 * Get global configuration from cache
 */
export async function getConfiguration(cacheName) {
    try {
        const db = getDatabase(cacheName);
        const tx = db.transaction(['configurationStore'], 'readonly');
        const store = tx.objectStore('configurationStore');
        const request = store.get(configurationKey);

        return new Promise((resolve, reject) => {
            request.onsuccess = () => {
                const entry = request.result;
                configurations[cacheName] = entry ? entry.data : null;
                resolve(entry ? entry.data : null);
            };
            request.onerror = () => {
                console.error('Get configuration error:', request.error);
                resolve(null);
            }
        });
    } catch (error) {
        console.error('Get configuration error:', error);
        return null;
    }
}

/**
 * Cleanup: remove expired entries and enforce size limits (excluding configuration)
 */
export async function cleanupDatabase(cacheName, maxCapacityInBytes, ttlSeconds, slidingTtlSeconds) {
    try {
        debugger;
        const db = getDatabase(cacheName);
        const tx = db.transaction(['cacheStore'], 'readwrite');
        const store = tx.objectStore('cacheStore');
        const indexByAccess = store.index('lastAccessedAt');
        const request = indexByAccess.getAll();

        return new Promise((resolve, reject) => {
            request.onsuccess = () => {
                const allEntries = request.result;
                
                const entries= allEntries;
                const now = Math.floor(Date.now() / 1000);
                const toDelete = [];
                let deletedAmountInBytes = 0;
                let totalSizeInBytes = 0;

                // First pass: remove expired entries
                entries.forEach((entry) => {
                    let isExpired = false;
                    totalSizeInBytes += (entry.size ?? 0);

                    // Check absolute TTL
                    let timeToLiveInSeconds = entry.timeToLiveInSeconds ?? ttlSeconds;
                    if (timeToLiveInSeconds) {
                        const age = now - entry.createdAt;
                        if (age > timeToLiveInSeconds) {
                            isExpired = true;
                        }
                    }

                    // Check sliding TTL
                    let slidingTimeToLiveInSeconds = entry.slidingTimeToLiveInSeconds ?? slidingTtlSeconds;
                    if (!isExpired && slidingTimeToLiveInSeconds) {
                        const timeSinceAccess = now - entry.lastAccessedAt;
                        if (timeSinceAccess > slidingTimeToLiveInSeconds) {
                            isExpired = true;
                        }
                    }

                    if (isExpired) {
                        toDelete.push(entry.key);
                        deletedAmountInBytes += (entry.size ?? 0);
                    }
                });

                // Delete expired entries
                toDelete.forEach(key => store.delete(key));

                // Second pass: if still over capacity, remove least recently accessed
                if (maxCapacityInBytes && totalSizeInBytes - deletedAmountInBytes > maxCapacityInBytes) {
                    let remainingEntries = entries.filter(e => !toDelete.includes(e.key));
                    
                    remainingEntries.sort((a, b) => a.lastAccessedAt - b.lastAccessedAt);
                    let totalSize = totalSizeInBytes - deletedAmountInBytes;
                    for (const entry of remainingEntries) {
                        if (totalSize <= maxCapacityInBytes) break;
                        store.delete(entry.key);
                        totalSize -= entry.size;
                        deletedAmountInBytes += entry.size;
                    }
                }

                tx.oncomplete = () => resolve(deletedAmountInBytes);
                tx.onerror = () => {
                    console.error('Cleanup error:', tx.error);
                    resolve(null);
                }
            };
            request.onerror = () => {
                console.error('Cleanup error:', request.error);
                resolve(null);
            }
        });
    } catch (error) {
        console.error('Cleanup error:', error);
        return null;
    }
}

/**
 * Get the total size of the cache (excluding configuration)
 */
export async function getTotalSize(cacheName) {
    try {
        const db = getDatabase(cacheName);
        const tx = db.transaction(['cacheStore'], 'readonly');
        const store = tx.objectStore('cacheStore');
        const request = store.getAll();

        return new Promise((resolve, reject) => {
            request.onsuccess = () => {
                const entries = request.result;
                const totalSize = entries
                    .reduce((sum, entry) => sum + entry.size, 0);
                resolve(totalSize);
            };
            request.onerror = () => {
                console.error('Size request error:', request.error);
                resolve(null);
            }
        });
    } catch (error) {
        console.error('Size error:', error);
        return null;
    }
}

/**
 * Retrieve entry from the cache
 */
export async function getEntry(cacheName, key) {
    try {
        const db = getDatabase(cacheName);
        const configuration = getConfigurationForCache(cacheName) ?? await getConfiguration(cacheName);
        const tx = db.transaction(['cacheStore'], 'readwrite');
        const store = tx.objectStore('cacheStore');
        const request = store.get(key);

        return new Promise((resolve, reject) => {
            request.onsuccess = () => {
                const entry = request.result;

                if (!entry) {
                    resolve(null);
                    return;
                }

                let isValidEntry = true;

                // Check if expired by absolute TTL
                let timeToLiveInSeconds = entry.timeToLiveInSeconds ?? configuration.timeToLiveInSeconds;
                if (timeToLiveInSeconds) {
                    const ageInSeconds = Math.floor(Date.now() / 1000) - entry.createdAt;
                    if (ageInSeconds > timeToLiveInSeconds) {
                        isValidEntry = false;
                    }
                }

                // Check if expired by sliding TTL
                let slidingTimeToLiveInSeconds = entry.slidingTimeToLiveInSeconds ?? configuration.slidingTimeToLiveInSeconds;
                if (slidingTimeToLiveInSeconds) {
                    const timeSinceLastAccess = Math.floor(Date.now() / 1000) - entry.lastAccessedAt;
                    if (timeSinceLastAccess > slidingTimeToLiveInSeconds) {
                        isValidEntry = false;
                    }
                }

                if (!isValidEntry) {
                    // Entry expired, delete it
                    const deleteTx = db.transaction(['cacheStore'], 'readwrite');
                    deleteTx.objectStore('cacheStore').delete(key);
                    resolve(null);
                    return;
                }

                entry.lastAccessedAt = Math.floor(Date.now() / 1000);
                store.put(entry);

                resolve(entry.data);
            };

            request.onerror = () => {
                console.error('Get error:', request.error);
                resolve(null);
            }
        });
    } catch (error) {
        console.error('Get error:', error);
        return null;
    }
}

/**
 * Save entry to cache
 */
export async function saveEntry(cacheName, key, data, size, configuration = null) {
    try {
        const db = await getDatabase(cacheName);
        const tx = db.transaction(['cacheStore'], 'readwrite');
        const store = tx.objectStore('cacheStore');

        const request = store.get(key);
        
        let deltaSize = 0;
        
        request.onsuccess = () => {
            let existingEntry = request.result;
            if (!existingEntry) {
                existingEntry = {
                    createdAt: Math.floor(Date.now() / 1000)
                }
            }
            deltaSize = (size ?? 0) - (existingEntry.size ?? 0);

            const newEntry = { key, data, size, lastAccessedAt: Math.floor(Date.now() / 1000) };
            if (configuration) {
                newEntry.timeToLiveInSeconds = configuration.timeToLiveInSeconds;
                newEntry.slidingTimeToLiveInSeconds = configuration.slidingTimeToLiveInSeconds;
            }

            existingEntry = Object.assign(existingEntry, newEntry);
            
            store.put(existingEntry);
        }
        
        return new Promise((resolve, reject) => {
            tx.oncomplete = () => resolve(deltaSize)
            tx.onabort = () => resolve(null);
            tx.onerror = () => {
                console.error('Save error:', tx.error);
                resolve(null);
            }
        });
    } catch (error) {
        console.error('Save error:', error);
        return null;
    }
}

/**
 * remove entry from the cache
 */
export async function removeEntry(cacheName, key) {
    try {
        const db = await getDatabase(cacheName);
        const tx = db.transaction(['cacheStore'], 'readwrite');
        const store = tx.objectStore('cacheStore');

        const request = store.get(key);

        let deltaSize = 0;

        request.onsuccess = () => {
            let existingEntry = request.result;
            if (!existingEntry) {
                existingEntry = {}
            }
            deltaSize = -(existingEntry.size ?? 0);

            store.delete(key);
        }

        return new Promise((resolve, reject) => {
            tx.oncomplete = () => resolve(deltaSize)
            tx.onabort = () => resolve(null);
            tx.onerror = () => {
                console.error('Save error:', tx.error);
                resolve(null);
            }
        });
    } catch (error) {
        console.error('Save error:', error);
        return null;
    }
}

/**
 * Get cache statistics
 */
export async function getCacheStats(cacheName, configKey) {
    try {
        const db = await getDatabase(cacheName);
        const tx = db.transaction(['cacheStore'], 'readonly');
        const store = tx.objectStore('cacheStore');
        const request = store.getAll();

        return new Promise((resolve, reject) => {
            request.onsuccess = () => {
                const entries = request.result
                    .filter(entry => entry.key !== configKey);
                const totalSize = entries.reduce((sum, entry) => sum + entry.size, 0);

                resolve({
                    itemCount: entries.length,
                    totalSizeInBytes: totalSize,
                });
            };
            request.onerror = () => reject(request.error);
        });
    } catch (error) {
        console.error('Stats error:', error);
        return { itemCount: 0, totalSizeInBytes: 0 };
    }
}
