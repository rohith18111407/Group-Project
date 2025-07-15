export interface FileNotification {
    id: string;
    fileName: string;
    fileExtension: string;
    filePath: string;
    fileSizeInBytes: number;
    category: number; // fileCategory (enum number)
    versionNumber: number;
    itemId: string;
    itemName: string;
    itemCategory: string;
    createdAt: string;
    createdBy: string;
    updatedAt?: string;
    updatedBy?: string;
}
